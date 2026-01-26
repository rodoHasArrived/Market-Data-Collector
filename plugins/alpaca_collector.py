#!/usr/bin/env python3
"""
Alpaca Market Data Collector

Reads configuration from stdin (JSON), outputs market data to stdout (JSON lines).
Errors and status messages go to stderr.

Usage:
    echo '{"key_id": "...", "secret_key": "...", "symbols": ["SPY"]}' | python alpaca_collector.py

Configuration schema:
    {
        "key_id": "ALPACA_KEY_ID",
        "secret_key": "ALPACA_SECRET_KEY",
        "use_paper": true,
        "symbols": ["SPY", "QQQ"],
        "subscription_type": "bars" | "trades" | "quotes"
    }

Output format (JSON lines):
    {"symbol": "SPY", "price": 450.25, "volume": 1500, "timestamp": "2024-01-19T15:30:00Z", "source": "alpaca"}
"""

import asyncio
import json
import sys
import signal
from datetime import datetime, timezone
from dataclasses import dataclass, asdict
from typing import Optional, Dict, Any

# Alpaca imports
try:
    from alpaca_trade_api import REST
    from alpaca_trade_api.stream import Stream
    ALPACA_AVAILABLE = True
except ImportError:
    ALPACA_AVAILABLE = False


@dataclass
class MarketData:
    """Market data point"""
    symbol: str
    price: float
    volume: int
    timestamp: str
    source: str = "alpaca"

    def to_json(self) -> str:
        return json.dumps(asdict(self))


def emit_data(data: MarketData) -> None:
    """Emit market data to stdout"""
    try:
        print(data.to_json(), flush=True)
    except Exception as e:
        emit_error(f"Failed to emit data: {e}")


def emit_status(status: str, **extra: Any) -> None:
    """Emit status message to stderr"""
    msg = {"status": status, "timestamp": datetime.now(timezone.utc).isoformat(), **extra}
    print(json.dumps(msg), file=sys.stderr, flush=True)


def emit_error(error: str) -> None:
    """Emit error message to stderr"""
    msg = {"error": error, "timestamp": datetime.now(timezone.utc).isoformat()}
    print(json.dumps(msg), file=sys.stderr, flush=True)


class AlpacaCollector:
    """Collects real-time market data from Alpaca"""

    def __init__(self, config: Dict[str, Any]):
        self.key_id = config.get("key_id")
        self.secret_key = config.get("secret_key")
        self.use_paper = config.get("use_paper", True)
        self.symbols = config.get("symbols", [])
        self.subscription_type = config.get("subscription_type", "bars")

        # Validate
        if not self.key_id:
            raise ValueError("key_id is required")
        if not self.secret_key:
            raise ValueError("secret_key is required")
        if not self.symbols:
            raise ValueError("symbols list is required")

        # API base URL
        self.base_url = (
            "https://paper-api.alpaca.markets"
            if self.use_paper
            else "https://api.alpaca.markets"
        )

        # Create REST client for verification and snapshots
        self.rest_client = REST(
            key_id=self.key_id,
            secret_key=self.secret_key,
            base_url=self.base_url
        )

        self.stream: Optional[Stream] = None
        self._running = True
        self._connected = False

    async def verify_connection(self) -> bool:
        """Verify API connection by checking account"""
        try:
            account = self.rest_client.get_account()
            emit_status("connected", account=account.account_number)
            return True
        except Exception as e:
            emit_error(f"Connection verification failed: {e}")
            return False

    async def get_snapshot(self) -> None:
        """Get latest data snapshot (non-streaming)"""
        try:
            bars = self.rest_client.get_latest_bars(self.symbols)
            for symbol, bar in bars.items():
                timestamp = bar.t.isoformat() if hasattr(bar.t, 'isoformat') else str(bar.t)
                data = MarketData(
                    symbol=symbol,
                    price=float(bar.c),  # Close price
                    volume=int(bar.v),
                    timestamp=timestamp
                )
                emit_data(data)
        except Exception as e:
            emit_error(f"Snapshot failed: {e}")

    def _on_bar(self, bar) -> None:
        """Handle incoming bar data"""
        if not self._running:
            return
        try:
            timestamp = bar.timestamp.isoformat() if hasattr(bar.timestamp, 'isoformat') else str(bar.timestamp)
            data = MarketData(
                symbol=bar.symbol,
                price=float(bar.close),
                volume=int(bar.volume),
                timestamp=timestamp
            )
            emit_data(data)
        except Exception as e:
            emit_error(f"Bar processing error: {e}")

    def _on_trade(self, trade) -> None:
        """Handle incoming trade data"""
        if not self._running:
            return
        try:
            timestamp = trade.timestamp.isoformat() if hasattr(trade.timestamp, 'isoformat') else str(trade.timestamp)
            data = MarketData(
                symbol=trade.symbol,
                price=float(trade.price),
                volume=int(trade.size),
                timestamp=timestamp
            )
            emit_data(data)
        except Exception as e:
            emit_error(f"Trade processing error: {e}")

    def _on_quote(self, quote) -> None:
        """Handle incoming quote data"""
        if not self._running:
            return
        try:
            # Use midpoint of bid/ask
            price = (float(quote.ask_price) + float(quote.bid_price)) / 2
            timestamp = quote.timestamp.isoformat() if hasattr(quote.timestamp, 'isoformat') else str(quote.timestamp)
            data = MarketData(
                symbol=quote.symbol,
                price=price,
                volume=0,  # Quotes don't have volume
                timestamp=timestamp
            )
            emit_data(data)
        except Exception as e:
            emit_error(f"Quote processing error: {e}")

    async def run_stream(self) -> None:
        """Run the WebSocket stream"""
        self.stream = Stream(
            api_key=self.key_id,
            secret_key=self.secret_key,
            base_url=self.base_url,
            data_feed="iex"  # Use IEX for free tier compatibility
        )

        # Subscribe based on type
        for symbol in self.symbols:
            if self.subscription_type == "bars":
                self.stream.subscribe_bars(self._on_bar, symbol)
            elif self.subscription_type == "trades":
                self.stream.subscribe_trades(self._on_trade, symbol)
            elif self.subscription_type == "quotes":
                self.stream.subscribe_quotes(self._on_quote, symbol)
            else:
                # Default to bars
                self.stream.subscribe_bars(self._on_bar, symbol)

        emit_status("streaming", symbols=self.symbols, type=self.subscription_type)

        try:
            await self.stream._run_forever()
        except asyncio.CancelledError:
            emit_status("stream_cancelled")
        except Exception as e:
            emit_error(f"Stream error: {e}")

    async def run(self) -> None:
        """Main run loop"""
        # Verify connection first
        if not await self.verify_connection():
            return

        # Get initial snapshot
        await self.get_snapshot()

        # Start streaming
        await self.run_stream()

    def stop(self) -> None:
        """Stop the collector"""
        self._running = False
        emit_status("shutting_down")

        if self.stream:
            try:
                for symbol in self.symbols:
                    self.stream.unsubscribe_bars(symbol)
                    self.stream.unsubscribe_trades(symbol)
                    self.stream.unsubscribe_quotes(symbol)
            except Exception as e:
                # Ignore unsubscription failures during shutdown, but log for diagnostics
                emit_error(f"Error while unsubscribing from Alpaca stream during shutdown: {e}")


async def main() -> None:
    """Entry point"""
    if not ALPACA_AVAILABLE:
        emit_error("alpaca-trade-api package not installed. Run: pip install alpaca-trade-api")
        sys.exit(1)

    # Read configuration from stdin
    try:
        config_text = sys.stdin.read()
        if not config_text.strip():
            emit_error("No configuration provided on stdin")
            sys.exit(1)

        config = json.loads(config_text)
    except json.JSONDecodeError as e:
        emit_error(f"Invalid JSON configuration: {e}")
        sys.exit(1)
    except Exception as e:
        emit_error(f"Failed to read configuration: {e}")
        sys.exit(1)

    # Create collector
    try:
        collector = AlpacaCollector(config)
    except ValueError as e:
        emit_error(f"Configuration error: {e}")
        sys.exit(1)

    # Set up signal handlers
    def handle_signal(sig, frame):
        collector.stop()

    signal.signal(signal.SIGTERM, handle_signal)
    signal.signal(signal.SIGINT, handle_signal)

    # Run collector
    try:
        await collector.run()
    except Exception as e:
        emit_error(f"Collector error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        emit_status("interrupted")
        sys.exit(0)
