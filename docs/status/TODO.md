# TODO Tracking

> Auto-generated TODO documentation. Do not edit manually.
> Last updated: 2026-02-12T17:25:12.984927+00:00

## Summary

| Metric | Count |
|--------|-------|
| **Total Items** | 21 |
| **Linked to Issues** | 0 |
| **Untracked** | 21 |

### By Type

| Type | Count | Description |
|------|-------|-------------|
| `NOTE` | 15 | Important notes and documentation |
| `TODO` | 6 | General tasks to complete |

### By Directory

| Directory | Count |
|-----------|-------|
| `tests/` | 18 |
| `src/` | 3 |

## Unassigned & Untracked

21 items have no assignee and no issue tracking:

Consider assigning ownership or creating tracking issues for these items.

## All Items

### TODO (6)

- [ ] `tests/MarketDataCollector.Ui.Tests/Collections/CircularBufferTests.cs:163`
  > Implement CalculatePercentageChange method in CircularBuffer<T> extension methods [Fact] public void CalculatePercentageChange_ReturnsCorrectValue() { // Arrange var buffer = new CircularBuffer<double>(capacity: 5); buffer.Add(100.0); buffer.Add(110.0);

- [ ] `tests/MarketDataCollector.Ui.Tests/Collections/CircularBufferTests.cs:179`
  > Implement CalculatePercentageChange method in CircularBuffer<T> extension methods [Fact] public void CalculatePercentageChange_WhenDivisionByZero_ReturnsNull() { // Arrange var buffer = new CircularBuffer<double>(capacity: 5); buffer.Add(0.0); buffer.Add(10.0);

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/FormValidationServiceTests.cs:13`
  > Implement ValidateSymbol tests when FormValidationRules is fully implemented [Theory] [InlineData("SPY", true)] [InlineData("AAPL", true)] [InlineData("MSFT", true)] [InlineData("TSLA", true)] [InlineData("", false)] [InlineData(null, false)] [InlineData("SP Y", false)] [InlineData("123", false)] [InlineData("A", true)] // Single letter symbols are valid (e.g., X, F) public void ValidateSymbol_ValidatesSymbolFormat(string? symbol, bool expectedValid) { // Act var result = FormValidationRules.ValidateSymbol(symbol);

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/FormValidationServiceTests.cs:37`
  > Implement ValidateDateRange tests - ValidateDate method doesn't exist yet [Theory] [InlineData("2024-01-01", true)] [InlineData("2024-12-31", true)] [InlineData("invalid", false)] [InlineData("", false)] [InlineData(null, false)] [InlineData("2024/01/01", false)] // Wrong format public void ValidateDateRange_ValidatesDateFormat(string? dateStr, bool expectedValid) { // Act var result = FormValidationRules.ValidateDateRange(dateStr);

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/FormValidationServiceTests.cs:58`
  > Implement ValidateFilePath tests when fully ready [Theory] [InlineData("config.json", true)] [InlineData("C:\\data\\config.json", true)] [InlineData("/var/data/config.json", true)] [InlineData("", false)] [InlineData(null, false)] public void ValidateFilePath_ValidatesPathFormat(string? path, bool expectedValid) { // Act var result = FormValidationRules.ValidateFilePath(path);

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/FormValidationServiceTests.cs:78`
  > Update to use int? instead of string parameter [Theory] [InlineData(8080, true)] [InlineData(80, true)] [InlineData(443, true)] [InlineData(65535, true)] [InlineData(0, false)] // Port 0 is reserved [InlineData(65536, false)] // Above max port [InlineData(-1, false)] [InlineData(null, false)] public void ValidatePort_ValidatesPortNumber(int? port, bool expectedValid) { // Act var result = FormValidationRules.ValidatePort(port);

### NOTE (15)

- [ ] `src/MarketDataCollector.Ui.Shared/Endpoints/ConfigEndpoints.cs:138`
  > Status endpoint is handled by StatusEndpoints.MapStatusEndpoints() which provides live status via StatusEndpointHandlers rather than loading from file

- [ ] `src/MarketDataCollector.Uwp/GlobalUsings.cs:7`
  > Type aliases and Contracts namespaces are NOT re-defined here because they are already provided by the referenced MarketDataCollector.Ui.Services project (via its GlobalUsings.cs). Re-defining them would cause CS0101 duplicate type definition errors. =============================================================================

- [ ] `src/MarketDataCollector.Wpf/GlobalUsings.cs:7`
  > Type aliases and Contracts namespaces are NOT re-defined here because they are already provided by the referenced MarketDataCollector.Ui.Services project (via its GlobalUsings.cs). Re-defining them would cause CS0101 duplicate type definition errors. =============================================================================

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:28`
  > Using null! because validation throws before dependencies are accessed

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:55`
  > Using null! because validation throws before dependencies are accessed

- [ ] `tests/MarketDataCollector.Tests/Application/Backfill/BackfillWorkerServiceTests.cs:84`
  > Using null! dependencies - we only verify that ArgumentOutOfRangeException is not thrown The constructor may throw other exceptions (e.g., NullReferenceException) when accessing null dependencies

- [ ] `tests/MarketDataCollector.Tests/Application/Commands/SymbolCommandsTests.cs:19`
  > SymbolCommands requires a SymbolManagementService which needs a ConfigStore. For CanHandle tests we can use a stub since CanHandle doesn't touch the service. For ExecuteAsync tests that require validation (missing value), we need the real command.

- [ ] `tests/MarketDataCollector.Tests/Application/Monitoring/DataQuality/DataFreshnessSlaMonitorTests.cs:523`
  > Actual result depends on current time, so we check the logic is working

- [ ] `tests/MarketDataCollector.Tests/Infrastructure/Resilience/WebSocketResiliencePolicyTests.cs:201`
  > We can't directly invoke the event from outside the class The test validates that the subscription mechanism works

- [ ] `tests/MarketDataCollector.Tests/Storage/StorageChecksumServiceTests.cs:121`
  > File.WriteAllTextAsync uses UTF-8 with BOM by default on some platforms, so we compute expected from the actual file bytes

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/BackfillServiceTests.cs:181`
  > This test verifies the IsRunning property logic In actual usage, CurrentProgress would be set during a backfill operation We're testing the property getter logic here

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/BackfillServiceTests.cs:198`
  > Similar to IsRunning test, this verifies the property logic

- [ ] `tests/MarketDataCollector.Ui.Tests/Services/WatchlistServiceTests.cs:175`
  > Tags property not yet implemented in WatchlistItem

- [ ] `tests/MarketDataCollector.Wpf.Tests/Services/ConnectionServiceTests.cs:286`
  > This test may need to wait briefly for async operation

- [ ] `tests/MarketDataCollector.Wpf.Tests/Services/NavigationServiceTests.cs:57`
  > This test assumes NavigationService might not be initialized In production, Initialize should be called during app startup

---

## Contributing

When adding TODO comments, please follow these guidelines:

1. **Link to GitHub Issues**: Use `// TODO: Track with issue #123` format
2. **Be Descriptive**: Explain what needs to be done and why
3. **Use Correct Type**:
   - `TODO` - General tasks
   - `FIXME` - Bugs that need fixing
   - `HACK` - Temporary workarounds
   - `NOTE` - Important information

Example:
```csharp
// TODO: Track with issue #123 - Implement retry logic for transient failures
// This is needed because the API occasionally returns 503 errors during peak load.
```
