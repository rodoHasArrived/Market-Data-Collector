# Troubleshooting Guide

This guide helps you diagnose and resolve common issues with MarketDataCollector.

## Specialized Troubleshooting Guides

For specific component issues, see:
- **[Desktop App (WinUI 3) XAML Compiler Errors](../troubleshooting/desktop-app-xaml-compiler-errors.md)** - XAML compilation failures, WindowsAppSDK issues

## Quick Diagnostics

### Health Check

Access the health endpoint to get a quick status:

```bash
curl http://localhost:8080/health
```

### Log Files

Check the log files for detailed error information:

```bash
tail -f data/_logs/mdc-*.log
```

### Metrics

View real-time metrics:

```bash
curl http://localhost:8080/metrics
```

## Configuration Issues

### "Configuration file not found"

**Symptom**: Warning about missing `appsettings.json`

**Solution**:
1. Copy the sample configuration:
   ```bash
   cp appsettings.sample.json appsettings.json
   ```
2. Edit with your settings

### "Invalid JSON in configuration file"

**Symptom**: Error parsing JSON with line/column information

**Solution**:
1. Check for common JSON errors:
   - Trailing commas (not allowed in JSON)
   - Missing quotes around strings
   - Unescaped characters
2. Validate your JSON:
   ```bash
   python -m json.tool appsettings.json
   ```
   Or use an online validator like [jsonlint.com](https://jsonlint.com)
3. Compare against `appsettings.sample.json`

### "Configuration validation failed"

**Symptom**: Startup fails with validation errors

**Solution**: Read the specific error messages and fix:

| Error | Fix |
|-------|-----|
| "DataRoot is required" | Set a valid `DataRoot` path |
| "Alpaca KeyId is required" | Add your Alpaca API key |
| "Feed must be 'iex' or 'sip'" | Change `Feed` to a valid value |
| "Symbol is required" | Add `Symbol` to each symbol config |
| "DepthLevels must be between 1 and 50" | Adjust `DepthLevels` value |

## Alpaca Issues

### "Alpaca KeyId/SecretKey required"

**Symptom**: Error about missing credentials when using Alpaca

**Solution**:
1. **Using environment variables** (recommended):
   ```bash
   export ALPACA_KEY_ID="your-key-id"
   export ALPACA_SECRET_KEY="your-secret-key"
   ```

2. **Using config file**:
   ```json
   {
     "Alpaca": {
       "KeyId": "AKXXXXXXXXXXXXXXXXXX",
       "SecretKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
     }
   }
   ```

### "Failed to connect to Alpaca WebSocket"

**Symptom**: WebSocket connection errors

**Troubleshooting steps**:

1. **Check internet connection**:
   ```bash
   curl -I https://stream.data.alpaca.markets
   ```

2. **Verify Alpaca service status**:
   - Check [status.alpaca.markets](https://status.alpaca.markets)

3. **Verify credentials**:
   - Log into Alpaca dashboard
   - Regenerate API keys if needed
   - Ensure keys are for the correct environment (live vs paper)

4. **Check firewall**:
   - Port 443 (HTTPS/WSS) must be open
   - Some corporate firewalls block WebSocket connections

5. **Try sandbox mode**:
   ```json
   {
     "Alpaca": {
       "UseSandbox": true
     }
   }
   ```

### "Authentication failed"

**Symptom**: WebSocket connects but auth message is rejected

**Solution**:
1. Verify your Key ID and Secret Key are correct
2. Check that you're using the right keys for the environment
3. Ensure your Alpaca account is active and in good standing
4. Try regenerating API keys in the Alpaca dashboard

### No trade data received

**Symptom**: Connected but no trades appearing

**Possible causes**:
1. **Market is closed** - Check market hours
2. **Wrong feed** - IEX feed may have less activity for some symbols
3. **Symbol not traded** - Try high-volume symbols like SPY or AAPL
4. **WebSocket disconnected** - Check logs for disconnect messages

## Interactive Brokers Issues

### "IB connection failed" / "Cannot connect"

**Symptom**: Unable to connect to TWS/Gateway

**Troubleshooting steps**:

1. **Verify TWS/Gateway is running**:
   - Check that TWS or IB Gateway is open and logged in
   - Look for the IB icon in your system tray

2. **Check API is enabled**:
   - In TWS: File → Global Configuration → API → Settings
   - Ensure "Enable ActiveX and Socket Clients" is checked

3. **Verify port number**:
   | Product | Default Port |
   |---------|--------------|
   | TWS Paper | 7497 |
   | TWS Live | 7496 |
   | Gateway Paper | 4002 |
   | Gateway Live | 4001 |

4. **Check trusted IPs**:
   - In API settings, add `127.0.0.1` or your IP to trusted list
   - Or disable "Allow connections from localhost only"

5. **Check firewall**:
   - Ensure the API port is not blocked
   - Try temporarily disabling firewall to test

6. **Read-only API**:
   - If you have "Read-Only API" checked, you can still get market data
   - This is recommended for data collection

### "No market data permissions"

**Symptom**: Connection works but no data for certain symbols

**Solution**:
1. Check market data subscriptions in Account Management
2. Ensure you have permission for the exchange (NYSE, NASDAQ, etc.)
3. Some symbols require specific subscriptions (e.g., Level 2 data)

### "Symbol not found" / "Contract ambiguity"

**Symptom**: IB can't find the symbol

**Solution**:
1. Use `LocalSymbol` for preferreds and complex symbols:
   ```json
   {
     "Symbol": "PCG-PA",
     "LocalSymbol": "PCG PRA"
   }
   ```

2. Use `ConId` if you know the exact contract ID:
   ```json
   {
     "Symbol": "SPY",
     "ConId": 756733
   }
   ```

3. Specify `PrimaryExchange` to disambiguate:
   ```json
   {
     "Symbol": "MSFT",
     "PrimaryExchange": "NASDAQ"
   }
   ```

### "Pacing violation"

**Symptom**: Too many requests error

**Solution**:
1. Reduce the number of symbols being subscribed
2. IB limits the rate of API requests
3. Wait and retry - the collector should recover automatically

## Data Quality Issues

### High drop rate

**Symptom**: `/health` shows elevated drop rate

**Diagnosis**:
```bash
curl -s http://localhost:8080/health | jq '.checks[] | select(.name=="drop_rate")'
```

**Solutions**:
1. Increase pipeline capacity:
   - This requires code changes to `EventPipeline` constructor
   - Default is 50,000 events

2. Reduce data volume:
   - Subscribe to fewer symbols
   - Disable depth subscription for high-volume symbols
   - Reduce `DepthLevels`

3. Improve system resources:
   - More RAM for larger queue
   - Faster disk for storage

### Integrity events

**Symptom**: Many integrity events in dashboard

**Types of integrity events**:

| Type | Cause | Action |
|------|-------|--------|
| Out-of-order | Network issues | Usually resolves itself |
| Sequence gap | Dropped packets | Check network quality |
| Level mismatch | Corrupt order book | Stream reset triggered automatically |

**Monitoring**:
```bash
curl -s http://localhost:8080/status | jq '.integrity'
```

### Stale data

**Symptom**: No new events for extended period

**Troubleshooting**:
1. Check connection status in logs
2. Verify market is open
3. Check network connectivity to provider
4. Restart the collector if connection seems stuck

## Performance Issues

### High memory usage

**Symptom**: Memory usage > 1GB

**Solutions**:
1. Enable compression to reduce storage footprint
2. Reduce queue size in EventPipeline
3. Reduce `DepthLevels` for symbols
4. Subscribe to fewer symbols

### Slow startup

**Symptom**: Takes a long time to start

**Possible causes**:
1. Large number of symbols to subscribe
2. Slow disk for log/data directory
3. Network latency to provider

### High CPU usage

**Symptom**: CPU consistently above 50%

**Solutions**:
1. Reduce symbol count
2. Disable depth for low-priority symbols
3. Increase flush interval to batch writes

## Storage Issues

### "Disk full" / No space

**Symptom**: Write failures or warnings about disk space

**Solutions**:
1. Enable retention policy:
   ```json
   {
     "Storage": {
       "RetentionDays": 30
     }
   }
   ```

2. Enable size limit:
   ```json
   {
     "Storage": {
       "MaxTotalMegabytes": 10240
     }
   }
   ```

3. Enable compression:
   ```json
   {
     "Compress": true
   }
   ```

4. Move `DataRoot` to larger disk

### "Permission denied"

**Symptom**: Cannot write to data directory

**Solution**:
```bash
# Check permissions
ls -la data/

# Fix permissions
chmod -R 755 data/
```

## Logging Issues

### No log output

**Symptom**: Console is empty or log files not created

**Solutions**:
1. Check `DataRoot` directory exists and is writable
2. Verify Serilog configuration:
   ```json
   {
     "Serilog": {
       "MinimumLevel": {
         "Default": "Information"
       }
     }
   }
   ```

3. Enable debug mode:
   ```bash
   export MDC_DEBUG=true
   ```

### Too much log output

**Symptom**: Logs filling up quickly

**Solution**:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning"
    }
  }
}
```

## Historical Backfill Issues

### "Backfill provider not found"

**Symptom**: Unknown provider error when starting backfill

**Solution**:
1. Use a valid provider ID: `alpaca`, `yahoo`, `stooq`, `nasdaq`, `composite`
2. Check provider configuration:
   ```json
   {
     "Backfill": {
       "Provider": "composite"
     }
   }
   ```

### "Rate limit exceeded"

**Symptom**: Backfill fails with rate limit errors

**Solutions**:
1. Enable rate limit rotation:
   ```json
   {
     "Backfill": {
       "RateLimitRotation": true,
       "EnableFallback": true
     }
   }
   ```
2. Use composite provider for automatic failover
3. Reduce batch size or add delay between requests

### "No data returned for symbol"

**Symptom**: Backfill completes but no data files created

**Possible causes**:
1. Symbol not supported by provider
2. Date range outside provider's data availability
3. Symbol delisted or changed

**Solution**: Try a different provider or check symbol validity

## Build and IDE Issues

### XAML Compiler Fails with "exited with code 1"

**Symptom**: Building the UWP desktop app fails with error:
```
error MSB3073: The command "XamlCompiler.exe" "obj\x64\Debug\...\win-x64\\input.json" exited with code 1
```

**Root Cause**: Windows App SDK 1.7.x XAML compiler has a path concatenation issue that creates double backslashes in intermediate output paths.

**Solution**: This issue is fixed in the project file as of commit f28815c. If you encounter this error:
1. Ensure you have the latest version of `src/MarketDataCollector.Uwp/MarketDataCollector.Uwp.csproj`
2. The fix normalizes `IntermediateOutputPath` to prevent trailing slashes
3. Clean and rebuild:
   ```bash
   dotnet clean src/MarketDataCollector.Uwp
   dotnet build src/MarketDataCollector.Uwp -c Release -r win-x64 -p:Platform=x64
   ```

**Reference**: GitHub Actions run 21609121398

### "The SDK 'Microsoft.NET.Sdk.Web' specified could not be found"

**Symptom**: `dotnet restore`/`dotnet build` or OmniSharp fails when loading microservice projects.

**Common causes**:
1. The .NET SDK version pinned in `global.json` is not installed.
2. The .NET SDK isn't installed at all (only the runtime is available).

**Solutions**:
1. Verify installed SDKs:
   ```bash
   dotnet --list-sdks
   ```
2. Install the SDK version in `global.json` (currently .NET 9.0), or update `global.json` to a version you already have installed.
3. Re-run `dotnet restore` after installation:
   ```bash
   dotnet restore
   ```

### "XamlCompiler.exe exited with code 1" (Desktop App)

**Symptom**: UWP/Desktop app build fails with error: `XamlCompiler.exe exited with code 1` without detailed error messages.

**Root Cause**: The WinUI 3 XAML compiler (`XamlCompiler.exe`) is a .NET Framework 4.7.2 (net472) executable that cannot process modern C# features like:
- C# 9 `record` types
- `init` accessors
- Global using directives
- Some System.Text.Json attributes

**Solution**: The project file should include:
```xml
<UseXamlCompilerExecutable>false</UseXamlCompilerExecutable>
```

This switches from the legacy net472 compiler to the managed (.NET) XAML compiler that understands modern C# syntax.

**Reference**: [Microsoft WinUI Issue #5315](https://github.com/microsoft/microsoft-ui-xaml/issues/5315)

**Additional Diagnostics**:
- Enable binary logging to inspect build details:
  ```bash
  dotnet build -bl:msbuild.binlog
  ```
- View the binary log using MSBuild Structured Log Viewer:
  - Download from: https://msbuildlog.com/
  - Open `msbuild.binlog` to see detailed build steps and failures

## Microservices Issues

### "Cannot connect to RabbitMQ"

**Symptom**: MassTransit fails to connect to message broker

**Solutions**:
1. Verify RabbitMQ is running:
   ```bash
   docker ps | grep rabbitmq
   ```
2. Check connection settings:
   ```json
   {
     "MassTransit": {
       "Transport": "RabbitMQ",
       "RabbitMQ": {
         "Host": "localhost",
         "Port": 5672
       }
     }
   }
   ```
3. Use InMemory transport for single-process testing:
   ```json
   {
     "MassTransit": {
       "Transport": "InMemory"
     }
   }
   ```

### "Service health check failing"

**Symptom**: Microservice shows unhealthy status

**Troubleshooting**:
```bash
# Check individual service health
curl http://localhost:5001/health

# View service logs
docker compose logs trade-ingestion
```

## UWP Desktop App Issues

### "Cannot find collector service"

**Symptom**: Desktop app can't connect to collector

**Solution**: Ensure the main collector is running with HTTP server:
```bash
dotnet run -- --http-port 8080
```

### "Configuration changes not saving"

**Symptom**: Settings don't persist after closing app

**Solution**:
1. Run app with administrator privileges
2. Check app's local data folder permissions
3. Verify `appsettings.json` is not read-only

## Getting Help

If you're still experiencing issues:

1. **Check the logs** - Most errors include detailed context
2. **Review documentation** - [Architecture](../architecture/overview.md), [Operator Runbook](operator-runbook.md)
3. **Run self-tests** - `dotnet run -- --selftest`
4. **Check backfill status** - `GET /api/backfill/status`
5. **File an issue** - Include logs, configuration (without secrets), and steps to reproduce

---

**Version:** 1.6.1
**Last Updated:** 2026-01-30
**See Also:** [Getting Started](getting-started.md) | [Configuration](configuration.md) | [Operator Runbook](operator-runbook.md) | [Lean Integration](../integrations/lean-integration.md)
