# Market Data Collector Plugins

This directory contains Python-based data collector plugins that are executed
as subprocesses by the main .NET application.

## Architecture

```
┌─────────────────────┐       stdin (JSON config)      ┌──────────────────────┐
│  .NET Application   │ ─────────────────────────────► │   Python Plugin      │
│                     │                                │   (subprocess)       │
│  PythonSubprocess   │ ◄───────────────────────────── │                      │
│  Provider           │       stdout (JSON lines)      │  alpaca_collector.py │
│                     │                                │                      │
│                     │ ◄───────────────────────────── │                      │
│                     │       stderr (status/errors)   │                      │
└─────────────────────┘                                └──────────────────────┘
```

## Installation

```bash
cd plugins
pip install -r requirements.txt
```

## Available Plugins

### alpaca_collector.py

Collects real-time market data from Alpaca Markets.

**Configuration:**
```json
{
    "key_id": "ALPACA_KEY_ID",
    "secret_key": "ALPACA_SECRET_KEY",
    "use_paper": true,
    "symbols": ["SPY", "QQQ", "AAPL"],
    "subscription_type": "bars"
}
```

**Environment Variables:**
- `ALPACA_KEY_ID` - Alpaca API key ID
- `ALPACA_SECRET_KEY` - Alpaca API secret key
- `ALPACA_USE_PAPER` - Use paper trading API (default: true)

**Subscription Types:**
- `bars` - OHLCV bars (default, ~1 per minute per symbol)
- `trades` - Trade-by-trade data (high volume)
- `quotes` - Best bid/offer quotes (high volume)

**Testing Standalone:**
```bash
echo '{"key_id": "your_key", "secret_key": "your_secret", "symbols": ["SPY"]}' | python alpaca_collector.py
```

## Output Format

All plugins emit JSON lines to stdout:

```json
{"symbol": "SPY", "price": 450.25, "volume": 1500, "timestamp": "2024-01-19T15:30:00Z", "source": "alpaca"}
{"symbol": "QQQ", "price": 380.50, "volume": 2000, "timestamp": "2024-01-19T15:30:01Z", "source": "alpaca"}
```

## Status/Error Messages

Status and error messages go to stderr as JSON:

```json
{"status": "connected", "account": "123456789", "timestamp": "2024-01-19T15:30:00Z"}
{"status": "streaming", "symbols": ["SPY", "QQQ"], "timestamp": "2024-01-19T15:30:01Z"}
{"error": "Connection failed: Invalid API key", "timestamp": "2024-01-19T15:30:00Z"}
```

## Creating New Plugins

1. Create a new Python file (e.g., `polygon_collector.py`)
2. Read JSON configuration from stdin
3. Emit market data as JSON lines to stdout
4. Emit status/errors to stderr
5. Handle SIGTERM/SIGINT for graceful shutdown

**Template:**
```python
#!/usr/bin/env python3
import json
import sys
import signal

def emit_data(symbol, price, volume, timestamp):
    print(json.dumps({
        "symbol": symbol,
        "price": price,
        "volume": volume,
        "timestamp": timestamp,
        "source": "my_source"
    }), flush=True)

def emit_error(error):
    print(json.dumps({
        "error": error,
        "timestamp": datetime.utcnow().isoformat()
    }), file=sys.stderr, flush=True)

def main():
    # Read config from stdin
    config = json.loads(sys.stdin.read())

    # Connect and stream data
    # emit_data(...)

if __name__ == "__main__":
    main()
```

## Integration with .NET

Plugins are configured in `appsettings.json`:

```json
{
  "Providers": [
    {
      "Name": "alpaca",
      "Type": "PythonSubprocess",
      "Enabled": true,
      "ScriptPath": "./plugins/alpaca_collector.py",
      "Symbols": ["SPY", "QQQ"]
    }
  ]
}
```

The `PythonSubprocessProvider` in .NET:
1. Spawns the Python process
2. Sends configuration via stdin
3. Reads market data from stdout
4. Monitors stderr for status/errors
5. Stores data in SQLite
