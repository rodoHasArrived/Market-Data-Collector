# QuantScriptEnvironment Blueprint

**Status:** Draft | **Author:** Claude (AI) | **Date:** 2026-03-18
**Effort:** XL (~3–4 weeks) | **Priority:** Medium-High
**ADR References:** ADR-004 (Async Streaming), ADR-006 (Domain Events), ADR-009 (F# Interop)

---

## 1. Scope

### In Scope

| Capability | Description |
|---|---|
| C# scripting | Edit and execute C# scripts against historical market data via Roslyn `CSharpScript` |
| Data access DSL | `Prices("SPY")`, `Returns("AAPL")`, `Indicator.SMA(prices, 20)` — thin wrappers over existing storage + Skender |
| Backtest bridge | One-click "Run Backtest" that compiles an `IBacktestStrategy` from the script and delegates to `BacktestEngine.RunAsync` |
| Statistics engine | Sharpe, Sortino, max drawdown, CAGR, Calmar, annualised vol, rolling beta, correlation matrix |
| Result plotting | Equity curve, drawdown chart, histogram of returns, overlay indicators — rendered via ScottPlot in the WPF page |
| Parameter sweep | `[ScriptParam]` attribute on globals → auto-generated UI form; optional grid-search over param ranges |
| WPF page | `QuantScriptPage` with AvalonEdit editor, tabbed results pane, parameter sidebar |
| Persistence | Save/load scripts to `data/_scripts/{name}.csx`; recent-scripts list |

### Out of Scope

| Item | Rationale |
|---|---|
| Live paper trading | Requires order routing; future feature |
| Multi-asset class (options, futures) | Equity-only first; extend via `IQuantDataContext` later |
| Cloud execution | Desktop-only; no remote compilation |
| IntelliSense / auto-complete | AvalonEdit does not ship a Roslyn completion provider; punt to v2 |
| Efficient frontier / portfolio optimisation | Listed as open question; defer unless trivial |

### Assumptions

1. The existing `BacktestEngine`, `IBacktestStrategy`, and `BacktestMetricsEngine` are stable and can be wrapped without modification.
2. `Microsoft.CodeAnalysis.CSharp` v5.0.0 is already in CPM; we add `Microsoft.CodeAnalysis.CSharp.Scripting` at the same version.
3. `Skender.Stock.Indicators` v2.7.1 is already in CPM and provides SMA, EMA, RSI, MACD, Bollinger, ATR, and 100+ others.
4. AvalonEdit and ScottPlot.WPF are not yet in CPM and must be added.
5. `BacktestMetricsEngine` is `internal static` inside `MarketDataCollector.Backtesting` — the new project must reference Backtesting (not just Sdk) or we add `[InternalsVisibleTo]`.

---

## 2. Architectural Overview

### Component Diagram (ASCII)

```
┌─────────────────────────────────────────────────────────────────────┐
│                        MarketDataCollector.Wpf                      │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ QuantScriptPage.xaml                                         │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌────────────────────┐  │   │
│  │  │ AvalonEdit   │  │ Results Tabs │  │ Parameter Sidebar  │  │   │
│  │  │ (Editor)     │  │ (ScottPlot)  │  │ (Auto-generated)   │  │   │
│  │  └──────┬──────┘  └──────▲───────┘  └────────▲───────────┘  │   │
│  └─────────┼────────────────┼───────────────────┼───────────────┘   │
│            │                │                   │                    │
│  ┌─────────▼────────────────┼───────────────────┼───────────────┐   │
│  │ QuantScriptViewModel : BindableBase                           │   │
│  │   • ScriptText, IsRunning, OutputLog, PlotModels              │   │
│  │   • RunCommand, StopCommand, SaveCommand, LoadCommand         │   │
│  └─────────┬────────────────┼───────────────────┼───────────────┘   │
└────────────┼────────────────┼───────────────────┼───────────────────┘
             │                │                   │
┌────────────▼────────────────┴───────────────────┴───────────────────┐
│                  MarketDataCollector.QuantScript                     │
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐  │
│  │ RoslynScript      │  │ ScriptRunner     │  │ QuantDataContext │  │
│  │ Compiler          │  │                  │  │                  │  │
│  │ (IQuantScript     │  │ (IScriptRunner)  │  │ (IQuantData      │  │
│  │  Compiler)        │  │                  │  │  Context)        │  │
│  └────────┬─────────┘  └────────┬─────────┘  └────────┬────────┘  │
│           │                     │                      │           │
│  ┌────────▼─────────────────────▼──────────────────────▼────────┐  │
│  │ QuantScriptGlobals                                            │  │
│  │  .Data   → DataProxy   (wraps IQuantDataContext)              │  │
│  │  .Test   → BacktestProxy (wraps BacktestEngine)               │  │
│  │  .Plot   → PlotQueue   (captures plot requests)               │  │
│  │  .Stats  → StatisticsEngine                                   │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐  │
│  │ PriceSeries /     │  │ PlotQueue /      │  │ Statistics       │  │
│  │ ReturnSeries      │  │ PlotRequest      │  │ Engine           │  │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘  │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ TechnicalSeriesExtensions  (SMA, EMA, RSI, MACD, Bollinger) │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
             │                     │                      │
             ▼                     ▼                      ▼
  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────────┐
  │ .Backtesting    │  │ .Application    │  │ .Storage             │
  │ BacktestEngine  │  │ HistoricalData  │  │ JsonlMarketDataStore │
  │ BacktestMetrics │  │ QueryService    │  │                      │
  └─────────────────┘  └─────────────────┘  └──────────────────────┘
```

### Design Decisions

| Decision | Rationale |
|---|---|
| Separate `MarketDataCollector.QuantScript` project | Keeps Roslyn dependency out of the main app; only WPF references it |
| Roslyn `CSharpScript.Create` (not `CSharpCompilation`) | Simpler API for scripting; supports globals object; handles `#r` directives |
| `PlotQueue` capture pattern | Script calls `Plot.Line(...)` which enqueues a `PlotRequest`; ViewModel drains queue after execution and renders via ScottPlot — decouples script from WPF |
| `DataProxy` / `BacktestProxy` wrappers | Provide a clean DSL (`Data.Prices("SPY")`) as async methods; Roslyn scripting supports `await` at the top level, so scripts use idiomatic `async/await` rather than blocking |
| `[ScriptParam]` attribute on globals fields | Enables auto-generated parameter UI without custom parsing |
| `InternalsVisibleTo` attribute on Backtesting project | Required to access `BacktestMetricsEngine` (internal static); alternative is making it public |

---

## 3. Interface & API Contracts

All interfaces and types below belong to `MarketDataCollector.QuantScript` namespace unless noted.

### 3.1 Data Series Types

```csharp
namespace MarketDataCollector.QuantScript.Series;

/// <summary>
/// Immutable time-indexed price series for a single symbol.
/// Wraps IReadOnlyList<HistoricalBar> with convenience accessors.
/// </summary>
public sealed class PriceSeries
{
    public string Symbol { get; }
    public IReadOnlyList<DateOnly> Dates { get; }
    public IReadOnlyList<decimal> Open { get; }
    public IReadOnlyList<decimal> High { get; }
    public IReadOnlyList<decimal> Low { get; }
    public IReadOnlyList<decimal> Close { get; }
    public IReadOnlyList<long> Volume { get; }
    public int Count { get; }

    public PriceSeries(string symbol, IReadOnlyList<HistoricalBar> bars);

    /// <summary>Slice by date range (inclusive).</summary>
    public PriceSeries Slice(DateOnly from, DateOnly to);

    /// <summary>Convert to Skender IQuote list for indicator calculation.</summary>
    public IReadOnlyList<IQuote> ToQuotes();

    /// <summary>Compute simple return series: (Close[i] - Close[i-1]) / Close[i-1].</summary>
    public ReturnSeries Returns();

    /// <summary>Compute log return series: ln(Close[i] / Close[i-1]).</summary>
    public ReturnSeries LogReturns();
}

/// <summary>
/// Immutable time-indexed return series.
/// </summary>
public sealed class ReturnSeries
{
    public string Symbol { get; }
    public IReadOnlyList<DateOnly> Dates { get; }
    public IReadOnlyList<double> Values { get; }
    public int Count { get; }

    public ReturnSeries(string symbol, IReadOnlyList<DateOnly> dates, IReadOnlyList<double> values);

    /// <summary>Annualised mean return (252 trading days).</summary>
    public double AnnualisedReturn();

    /// <summary>Annualised standard deviation.</summary>
    public double AnnualisedVolatility();

    /// <summary>Sharpe ratio (risk-free rate parameter, default 0).</summary>
    public double SharpeRatio(double riskFreeRate = 0.0);

    /// <summary>Maximum drawdown as a positive fraction.</summary>
    public double MaxDrawdown();
}
```

### 3.2 Data Context

```csharp
namespace MarketDataCollector.QuantScript.Data;

/// <summary>
/// Provides access to historical market data for scripts.
/// Implementations load data from IMarketDataStore / IHistoricalDataProvider.
/// </summary>
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public interface IQuantDataContext
{
    /// <summary>Load daily OHLCV bars as a PriceSeries.</summary>
    Task<PriceSeries> GetPricesAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <summary>Load return series (simple returns).</summary>
    Task<ReturnSeries> GetReturnsAsync(
        string symbol,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default);

    /// <summary>List symbols with available historical data.</summary>
    Task<IReadOnlyList<string>> GetAvailableSymbolsAsync(CancellationToken ct = default);
}
```

### 3.3 Script Compiler

```csharp
namespace MarketDataCollector.QuantScript.Compilation;

/// <summary>
/// Result of script compilation.
/// </summary>
public sealed class CompilationResult
{
    public bool Success { get; init; }
    public IReadOnlyList<CompilationDiagnostic> Diagnostics { get; init; } = [];
    public Script<object>? CompiledScript { get; init; }
}

public sealed record CompilationDiagnostic(
    string Id,
    string Message,
    int Line,
    int Column,
    DiagnosticSeverityLevel Severity);

public enum DiagnosticSeverityLevel { Info, Warning, Error }

/// <summary>
/// Compiles C# script text into an executable script.
/// </summary>
public interface IQuantScriptCompiler
{
    /// <summary>
    /// Compile script source code. Returns diagnostics on failure.
    /// The compiled script is bound to QuantScriptGlobals.
    /// </summary>
    CompilationResult Compile(string sourceCode);

    /// <summary>
    /// Detect [ScriptParam] attributes in the source to build parameter UI.
    /// </summary>
    IReadOnlyList<ScriptParameterInfo> ExtractParameters(string sourceCode);
}
```

### 3.4 Script Runner

```csharp
namespace MarketDataCollector.QuantScript.Execution;

/// <summary>
/// Result of a script execution.
/// </summary>
public sealed class ScriptExecutionResult
{
    public bool Success { get; init; }
    public object? ReturnValue { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public TimeSpan Elapsed { get; init; }
    public IReadOnlyList<PlotRequest> Plots { get; init; } = [];
    public IReadOnlyList<string> LogMessages { get; init; } = [];
}

/// <summary>
/// Executes compiled scripts with a globals object and cancellation support.
/// </summary>
[ImplementsAdr("ADR-004", "All async methods support CancellationToken")]
public interface IScriptRunner
{
    /// <summary>
    /// Execute a compiled script.
    /// </summary>
    /// <param name="compiledScript">Output of IQuantScriptCompiler.Compile</param>
    /// <param name="globals">The globals instance (QuantScriptGlobals)</param>
    /// <param name="ct">Cancellation token for long-running scripts</param>
    Task<ScriptExecutionResult> RunAsync(
        Script<object> compiledScript,
        QuantScriptGlobals globals,
        CancellationToken ct = default);
}
```

### 3.5 Plotting

```csharp
namespace MarketDataCollector.QuantScript.Plotting;

public enum PlotType
{
    Line,
    Scatter,
    Bar,
    Histogram,
    Area,
    Candlestick
}

/// <summary>
/// A single plot request captured during script execution.
/// </summary>
public sealed record PlotRequest(
    string Title,
    PlotType Type,
    string? XLabel,
    string? YLabel,
    IReadOnlyList<double> XValues,
    IReadOnlyList<double> YValues,
    string? SeriesLabel = null,
    string? Color = null);

/// <summary>
/// Accumulates plot requests from script code.
/// Thread-safe; scripts call Plot.Line(...) etc.
/// </summary>
public sealed class PlotQueue
{
    private readonly ConcurrentQueue<PlotRequest> _queue = new();

    public void Line(string title, IReadOnlyList<double> x, IReadOnlyList<double> y,
        string? label = null, string? color = null)
    {
        _queue.Enqueue(new PlotRequest(title, PlotType.Line, null, null, x, y, label, color));
    }

    public void Scatter(string title, IReadOnlyList<double> x, IReadOnlyList<double> y,
        string? label = null, string? color = null)
    {
        _queue.Enqueue(new PlotRequest(title, PlotType.Scatter, null, null, x, y, label, color));
    }

    public void Histogram(string title, IReadOnlyList<double> values,
        string? label = null)
    {
        _queue.Enqueue(new PlotRequest(title, PlotType.Histogram, null, null,
            values, [], label));
    }

    public void Bar(string title, IReadOnlyList<double> x, IReadOnlyList<double> y,
        string? label = null, string? color = null)
    {
        _queue.Enqueue(new PlotRequest(title, PlotType.Bar, null, null, x, y, label, color));
    }

    public void Area(string title, IReadOnlyList<double> x, IReadOnlyList<double> y,
        string? label = null, string? color = null)
    {
        _queue.Enqueue(new PlotRequest(title, PlotType.Area, null, null, x, y, label, color));
    }

    /// <summary>Drain all queued requests.</summary>
    public IReadOnlyList<PlotRequest> DrainAll();

    /// <summary>Clear all queued requests.</summary>
    public void Clear();
}
```

### 3.6 Statistics Engine

```csharp
namespace MarketDataCollector.QuantScript.Statistics;

/// <summary>
/// Portfolio-level statistics computed from return series or backtest results.
/// </summary>
public sealed record PortfolioStatistics
{
    public double AnnualisedReturn { get; init; }
    public double AnnualisedVolatility { get; init; }
    public double SharpeRatio { get; init; }
    public double SortinoRatio { get; init; }
    public double MaxDrawdown { get; init; }
    public double MaxDrawdownDuration { get; init; } // trading days
    public double Cagr { get; init; }
    public double CalmarRatio { get; init; }
    public double Skewness { get; init; }
    public double Kurtosis { get; init; }
    public double WinRate { get; init; }
    public double ProfitFactor { get; init; }
    public int TotalTrades { get; init; }
}

/// <summary>
/// Correlation matrix for multiple return series.
/// </summary>
public sealed class CorrelationMatrix
{
    public IReadOnlyList<string> Symbols { get; }
    public double[,] Values { get; }

    public CorrelationMatrix(IReadOnlyList<string> symbols, double[,] values);

    public double this[string a, string b] { get; }
}

/// <summary>
/// Computes portfolio and series statistics.
/// </summary>
public sealed class StatisticsEngine
{
    /// <summary>Compute full statistics from a return series.</summary>
    public PortfolioStatistics Compute(ReturnSeries returns, double riskFreeRate = 0.0);

    /// <summary>Compute correlation matrix across multiple series.</summary>
    public CorrelationMatrix Correlation(IReadOnlyList<ReturnSeries> series);

    /// <summary>Compute rolling beta vs a benchmark.</summary>
    public IReadOnlyList<double> RollingBeta(
        ReturnSeries asset,
        ReturnSeries benchmark,
        int windowDays = 60);
}
```

### 3.7 Script Globals & Proxies

```csharp
namespace MarketDataCollector.QuantScript.Execution;

/// <summary>
/// The globals object injected into every script.
/// Scripts access these as top-level variables: Data.Prices("SPY"), Plot.Line(...), etc.
/// </summary>
public sealed class QuantScriptGlobals
{
    public required DataProxy Data { get; init; }
    public required BacktestProxy Test { get; init; }
    public required PlotQueue Plot { get; init; }
    public required StatisticsEngine Stats { get; init; }
    public required Action<string> Log { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Async data access proxy over IQuantDataContext for use inside scripts.
/// Roslyn CSharpScript fully supports async/await at the top level, so
/// scripts call these methods with await directly.
/// </summary>
public sealed class DataProxy
{
    private readonly IQuantDataContext _ctx;
    private readonly CancellationToken _ct;

    public DataProxy(IQuantDataContext ctx, CancellationToken ct);

    /// <summary>Load daily OHLCV bars.</summary>
    public Task<PriceSeries> PricesAsync(string symbol, DateOnly? from = null, DateOnly? to = null)
        => _ctx.GetPricesAsync(symbol, from, to, _ct);

    /// <summary>Load simple return series.</summary>
    public Task<ReturnSeries> ReturnsAsync(string symbol, DateOnly? from = null, DateOnly? to = null)
        => _ctx.GetReturnsAsync(symbol, from, to, _ct);

    /// <summary>List available symbols.</summary>
    public Task<IReadOnlyList<string>> SymbolsAsync()
        => _ctx.GetAvailableSymbolsAsync(_ct);
}

/// <summary>
/// Wraps BacktestEngine for script-friendly backtest execution.
/// </summary>
public sealed class BacktestProxy
{
    private readonly BacktestEngine _engine;
    private readonly CancellationToken _ct;

    public BacktestProxy(BacktestEngine engine, CancellationToken ct);

    /// <summary>
    /// Run a backtest with the given strategy.
    /// Scripts implement IBacktestStrategy directly in their code.
    /// </summary>
    public Task<BacktestResult> RunAsync(BacktestRequest request, IBacktestStrategy strategy)
        => _engine.RunAsync(request, strategy, progress: null, _ct);
}
```

### 3.8 Script Parameter Discovery

```csharp
namespace MarketDataCollector.QuantScript.Compilation;

/// <summary>
/// Attribute scripts place on global fields to declare tuneable parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ScriptParamAttribute : Attribute
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public double Min { get; init; } = double.MinValue;
    public double Max { get; init; } = double.MaxValue;
    public double Step { get; init; } = 1.0;
}

/// <summary>
/// Metadata extracted from a [ScriptParam]-annotated field.
/// </summary>
public sealed record ScriptParameterInfo(
    string Name,
    string DisplayName,
    string? Description,
    Type ParameterType,
    object? DefaultValue,
    double Min,
    double Max,
    double Step);
```

### 3.9 Configuration Options

```csharp
namespace MarketDataCollector.QuantScript;

/// <summary>
/// Options bound from appsettings.json section "QuantScript".
/// </summary>
public sealed class QuantScriptOptions
{
    public const string SectionName = "QuantScript";

    /// <summary>Max script execution time before auto-cancel.</summary>
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Directory for saved scripts.</summary>
    public string ScriptsDirectory { get; set; } = "data/_scripts";

    /// <summary>Maximum number of recent scripts to track.</summary>
    public int MaxRecentScripts { get; set; } = 20;

    /// <summary>Default risk-free rate for Sharpe calculations.</summary>
    public double DefaultRiskFreeRate { get; set; } = 0.0;

    /// <summary>Assemblies to auto-import in scripts.</summary>
    public IReadOnlyList<string> DefaultImports { get; set; } =
    [
        "System",
        "System.Linq",
        "System.Collections.Generic",
        "System.Threading.Tasks",
        "MarketDataCollector.QuantScript.Series",
        "MarketDataCollector.QuantScript.Statistics",
        "MarketDataCollector.QuantScript.Plotting",
        "MarketDataCollector.Backtesting.Sdk",
        "Skender.Stock.Indicators"
    ];
}
```

### 3.10 Technical Indicator Extensions

```csharp
namespace MarketDataCollector.QuantScript.Indicators;

/// <summary>
/// Fluent extension methods bridging PriceSeries → Skender indicators → double[].
/// </summary>
public static class TechnicalSeriesExtensions
{
    public static IReadOnlyList<double?> Sma(this PriceSeries series, int periods)
        => series.ToQuotes().GetSma(periods).Select(r => r.Sma).ToList();

    public static IReadOnlyList<double?> Ema(this PriceSeries series, int periods)
        => series.ToQuotes().GetEma(periods).Select(r => r.Ema).ToList();

    public static IReadOnlyList<double?> Rsi(this PriceSeries series, int periods = 14)
        => series.ToQuotes().GetRsi(periods).Select(r => r.Rsi).ToList();

    public static IReadOnlyList<(double? Macd, double? Signal, double? Histogram)>
        Macd(this PriceSeries series, int fast = 12, int slow = 26, int signal = 9)
        => series.ToQuotes().GetMacd(fast, slow, signal)
            .Select(r => (r.Macd, r.Signal, r.Histogram)).ToList();

    public static IReadOnlyList<(double? Upper, double? Middle, double? Lower)>
        BollingerBands(this PriceSeries series, int periods = 20, double stdDevs = 2.0)
        => series.ToQuotes().GetBollingerBands(periods, stdDevs)
            .Select(r => (r.UpperBand, r.Sma, r.LowerBand)).ToList();

    public static IReadOnlyList<double?> Atr(this PriceSeries series, int periods = 14)
        => series.ToQuotes().GetAtr(periods).Select(r => r.Atr).ToList();
}
```

---

## 4. Component Design

### 4.1 QuantDataContext

**Class:** `MarketDataCollector.QuantScript.Data.QuantDataContext : IQuantDataContext`

**Dependencies:** `HistoricalDataQueryService`, `IMarketDataStore`

**Behaviour:**
- `GetPricesAsync` delegates to `HistoricalDataQueryService` (which queries `JsonlMarketDataStore` or triggers backfill).
- Caches loaded bars in a `ConcurrentDictionary<(string, DateOnly?, DateOnly?), PriceSeries>` to avoid repeated I/O within the same script execution.
- Cache is scoped to a single script run; a new `QuantDataContext` is created per execution.

### 4.2 RoslynScriptCompiler

**Class:** `MarketDataCollector.QuantScript.Compilation.RoslynScriptCompiler : IQuantScriptCompiler`

**Dependencies:** `IOptions<QuantScriptOptions>`

**Behaviour:**
- Uses `CSharpScript.Create<object>(source, options, globalsType: typeof(QuantScriptGlobals))`.
- `ScriptOptions` configured with:
  - References: all assemblies from `QuantScriptGlobals`' transitive closure plus `Skender.Stock.Indicators`.
  - Imports: from `QuantScriptOptions.DefaultImports`.
  - `WithEmitDebugInformation(true)` for stack traces with line numbers.
- `ExtractParameters` uses Roslyn syntax tree parsing:
  1. Parse source with `CSharpSyntaxTree.ParseText`.
  2. Walk field declarations looking for `[ScriptParam]` attribute.
  3. Extract name, type, default value, and attribute properties.

### 4.3 ScriptRunner

**Class:** `MarketDataCollector.QuantScript.Execution.ScriptRunner : IScriptRunner`

**Dependencies:** `ILogger<ScriptRunner>`, `IOptions<QuantScriptOptions>`

**Behaviour:**
- Runs the compiled script on `Task.Run` (background thread) to avoid blocking the UI.
- Creates a `CancellationTokenSource` linked to both the caller's token and `ExecutionTimeout`.
- Wraps execution in try/catch; captures `CompilationErrorException` and general `Exception`.
- After execution, drains `PlotQueue` and collects log messages into `ScriptExecutionResult`.
- Disposes the linked `CancellationTokenSource` in a finally block.

### 4.4 PlotQueue

Already fully specified in §3.5. Implementation uses `ConcurrentQueue<PlotRequest>` and `DrainAll()` dequeues all items into a list.

### 4.5 StatisticsEngine

**Class:** `MarketDataCollector.QuantScript.Statistics.StatisticsEngine`

**Key formulas (252 trading days/year):**

| Metric | Formula |
|---|---|
| Annualised Return | `mean(r) × 252` |
| Annualised Vol | `stddev(r) × √252` |
| Sharpe | `(annRet - rf) / annVol` |
| Sortino | `(annRet - rf) / (downsideDev × √252)` |
| Max Drawdown | `max(peak - trough) / peak` over cumulative returns |
| CAGR | `(endValue / startValue)^(252/N) - 1` |
| Calmar | `CAGR / maxDrawdown` |
| Skewness / Kurtosis | Standard statistical moments |
| Win Rate | `countPositive / total` |
| Profit Factor | `sumPositive / abs(sumNegative)` |
| Correlation | Pearson correlation coefficient |
| Rolling Beta | `cov(asset, bench, window) / var(bench, window)` |

### 4.6 TechnicalSeriesExtensions

Already fully specified in §3.10. Pure static extension methods, no state.

### 4.7 QuantScriptViewModel

**Class:** `MarketDataCollector.Wpf.ViewModels.QuantScriptViewModel : BindableBase`

**Dependencies (injected):** `IQuantScriptCompiler`, `IScriptRunner`, `IQuantDataContext`, `BacktestEngine`, `IOptions<QuantScriptOptions>`, `ILogger<QuantScriptViewModel>`

**Properties:**

| Property | Type | Binding |
|---|---|---|
| `ScriptText` | `string` | TwoWay to AvalonEdit |
| `IsRunning` | `bool` | OneWay, controls Run/Stop button state |
| `OutputLog` | `ObservableCollection<string>` | OneWay to log ListBox |
| `Diagnostics` | `ObservableCollection<CompilationDiagnostic>` | OneWay to errors DataGrid |
| `PlotRequests` | `ObservableCollection<PlotRequest>` | OneWay, drives ScottPlot rendering |
| `Statistics` | `PortfolioStatistics?` | OneWay to stats panel |
| `Parameters` | `ObservableCollection<ScriptParameterViewModel>` | TwoWay for parameter sidebar |
| `SelectedScriptPath` | `string?` | OneWay, current file path |
| `RecentScripts` | `ObservableCollection<string>` | OneWay to recent scripts dropdown |
| `StatusMessage` | `string` | OneWay to status bar |

**Commands:**

| Command | Behaviour |
|---|---|
| `RunCommand` | Compile → extract params → build globals → run → drain plots → update UI |
| `StopCommand` | Cancel the `CancellationTokenSource` |
| `SaveCommand` | Write `ScriptText` to `ScriptsDirectory/{name}.csx` |
| `LoadCommand` | Read `.csx` file into `ScriptText` |
| `NewCommand` | Reset `ScriptText` to template |

**Run flow (on `RunCommand`):**

1. Set `IsRunning = true`, clear outputs.
2. Call `_compiler.Compile(ScriptText)`.
3. If compilation fails, populate `Diagnostics`, set `IsRunning = false`, return.
4. Extract parameters; merge with user-supplied values from `Parameters` collection.
5. Create `QuantScriptGlobals` with `DataProxy`, `BacktestProxy`, `PlotQueue`, `StatisticsEngine`.
6. Call `_runner.RunAsync(compiled, globals, cts.Token)` on background.
7. On completion, marshal to UI thread:
   - Populate `PlotRequests` from result.
   - Populate `OutputLog` from result.
   - If backtest ran, populate `Statistics`.
8. Set `IsRunning = false`.

---

## 5. Data Flow

### Path 1: Data Analysis Script

```
User writes:  var spy = await Data.PricesAsync("SPY");
              var sma = spy.SMA(20);
              var stats = Stats.Compute(spy.Returns());
              Plot.Line(
                  "SPY Close",
                  spy.Dates.Select(d => d.ToOADate()).ToArray(),
                  spy.Close.Select(c => (double)c).ToArray());
              Plot.Line(
                  "SMA 20",
                  spy.Dates.Select(d => d.ToOADate()).ToArray(),
                  sma.Select(x => (double)x).ToArray());

Execution:
  ScriptText ──► RoslynScriptCompiler.Compile()
                     │
                     ▼ Script<object>
               ScriptRunner.RunAsync(script, globals)
                     │
                     ├─► await globals.Data.PricesAsync("SPY")
                     │       └─► QuantDataContext.GetPricesAsync
                     │               └─► HistoricalDataQueryService
                     │                       └─► JsonlMarketDataStore.QueryAsync
                     │                               └─► returns IReadOnlyList<HistoricalBar>
                     │               └─► new PriceSeries("SPY", bars)
                     │
                     ├─► spy.SMA(20)
                     │       └─► TechnicalSeriesExtensions.SMA
                     │               └─► Skender GetSma(20)
                     │
                     ├─► Stats.Compute(spy.Returns())
                     │       └─► ReturnSeries.Returns() → StatisticsEngine.Compute
                     │
                     ├─► Plot.Line("SPY Close", ...)
                     │       └─► PlotQueue.Enqueue(PlotRequest)
                     │
                     └─► Plot.Line("SMA 20", ...)
                             └─► PlotQueue.Enqueue(PlotRequest)

  ScriptRunner returns ScriptExecutionResult { Plots: [2 items] }

  QuantScriptViewModel:
    ├─► PlotRequests.AddRange(result.Plots)
    ├─► Statistics = stats  (if returned)
    └─► ScottPlot renders Line plots in WPF
```

### Path 2: Backtest Script

```
User writes:  class MyStrategy : IBacktestStrategy { ... OnBar(...) { ... } }
              var req = new BacktestRequest { ... };
              var result = await Test.RunAsync(req, new MyStrategy());
              var stats = Stats.Compute(result.Returns);
              Plot.Line("Equity", result.EquityCurve);

Execution:
  ScriptText ──► RoslynScriptCompiler.Compile()
                     │
                     ▼ Script<object>
               ScriptRunner.RunAsync(script, globals)
                     │
                     ├─► new MyStrategy()     [compiled in-script]
                     │
                     ├─► await globals.Test.RunAsync(req, strategy)
                     │       └─► BacktestProxy.RunAsync
                     │               └─► BacktestEngine.RunAsync(req, strategy, null, ct)
                     │                       ├─► MultiSymbolMergeEnumerator
                     │                       ├─► strategy.OnBar() [called per bar]
                     │                       ├─► SimulatedPortfolio fills
                     │                       └─► BacktestMetricsEngine.Calculate
                     │               └─► returns BacktestResult
                     │
                     ├─► Stats.Compute(result.Returns)
                     │
                     └─► Plot.Line("Equity", ...)
                             └─► PlotQueue.Enqueue(PlotRequest)

  QuantScriptViewModel renders equity curve + stats table
```

---

## 6. XAML Design

### Layout: 3-Column with Splitters

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ Toolbar: [New] [Open ▼] [Save] [Save As]  ║  [▶ Run] [■ Stop]  ║ Status   │
├───────────────────────┬──────────────────────────────┬───────────────────────┤
│                       │                              │                       │
│  AvalonEdit           │  Results TabControl          │  Parameters           │
│  (C# syntax           │  ┌────┬────┬────┬────┐      │                       │
│   highlighting)       │  │Plot│Log │Err │Stats│     │  [ScriptParam] fields  │
│                       │  ├────┴────┴────┴────┤      │  auto-generated:       │
│  Width: 2*            │  │                    │      │                       │
│                       │  │ ScottPlot chart    │      │  LookbackPeriod: [20] │
│                       │  │ or DataGrid        │      │  StopLoss%:    [0.02] │
│                       │  │ or TextBlock log   │      │  TakeProfit%:  [0.05] │
│                       │  │                    │      │                       │
│                       │  │ Width: 3*          │      │  Width: 1*            │
│                       │  │                    │      │                       │
│                       │  │                    │      │  ─────────────────── │
│                       │  │                    │      │  Backtest Result:     │
│                       │  │                    │      │  Sharpe: 1.42         │
│                       │  │                    │      │  MaxDD: -12.3%        │
│                       │  │                    │      │  CAGR: 15.2%          │
│                       │  │                    │      │  Trades: 147          │
│                       │  │                    │      │                       │
├───────────────────────┴──────────────────────────────┴───────────────────────┤
│ StatusBar: Ready │ Last run: 2.3s │ SPY: 1,258 bars loaded                  │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Key XAML Elements

```xml
<!-- QuantScriptPage.xaml (conceptual) -->
<Page x:Class="MarketDataCollector.Wpf.Views.QuantScriptPage">
  <DockPanel>
    <!-- Toolbar -->
    <ToolBarTray DockPanel.Dock="Top">
      <ToolBar>
        <Button Content="New" Command="{Binding NewCommand}" />
        <ComboBox ItemsSource="{Binding RecentScripts}"
                  SelectedItem="{Binding SelectedScriptPath}" />
        <Button Content="Save" Command="{Binding SaveCommand}" />
        <Separator />
        <Button Content="▶ Run" Command="{Binding RunCommand}"
                IsEnabled="{Binding IsRunning, Converter={StaticResource InvertBool}}" />
        <Button Content="■ Stop" Command="{Binding StopCommand}"
                IsEnabled="{Binding IsRunning}" />
      </ToolBar>
    </ToolBarTray>

    <!-- StatusBar -->
    <StatusBar DockPanel.Dock="Bottom">
      <TextBlock Text="{Binding StatusMessage}" />
    </StatusBar>

    <!-- 3-column grid -->
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="2*" MinWidth="200" />
        <ColumnDefinition Width="Auto" /> <!-- GridSplitter -->
        <ColumnDefinition Width="3*" MinWidth="300" />
        <ColumnDefinition Width="Auto" /> <!-- GridSplitter -->
        <ColumnDefinition Width="1*" MinWidth="150" />
      </Grid.ColumnDefinitions>

      <!-- Editor: AvalonEdit -->
      <avalonEdit:TextEditor Grid.Column="0"
                             SyntaxHighlighting="C#"
                             ShowLineNumbers="True"
                             FontFamily="Cascadia Code"
                             FontSize="13" />

      <GridSplitter Grid.Column="1" Width="5" />

      <!-- Results: TabControl -->
      <TabControl Grid.Column="2">
        <TabItem Header="Plot">
          <scottPlot:WpfPlot x:Name="ResultPlot" />
        </TabItem>
        <TabItem Header="Log">
          <ListBox ItemsSource="{Binding OutputLog}" />
        </TabItem>
        <TabItem Header="Errors">
          <DataGrid ItemsSource="{Binding Diagnostics}" IsReadOnly="True"
                    AutoGenerateColumns="False">
            <DataGrid.Columns>
              <DataGridTextColumn Header="Line" Binding="{Binding Line}" Width="50" />
              <DataGridTextColumn Header="Severity" Binding="{Binding Severity}" Width="60" />
              <DataGridTextColumn Header="Message" Binding="{Binding Message}" Width="*" />
            </DataGrid.Columns>
          </DataGrid>
        </TabItem>
        <TabItem Header="Stats">
          <ScrollViewer>
            <ItemsControl ItemsSource="{Binding StatisticsItems}" />
          </ScrollViewer>
        </TabItem>
      </TabControl>

      <GridSplitter Grid.Column="3" Width="5" />

      <!-- Parameters sidebar -->
      <ScrollViewer Grid.Column="4">
        <StackPanel>
          <TextBlock Text="Parameters" FontWeight="Bold" Margin="5" />
          <ItemsControl ItemsSource="{Binding Parameters}">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <StackPanel Margin="5">
                  <TextBlock Text="{Binding DisplayName}" />
                  <TextBox Text="{Binding Value, Mode=TwoWay}" />
                </StackPanel>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
          <Separator Margin="5,10" />
          <TextBlock Text="Statistics" FontWeight="Bold" Margin="5" />
          <!-- Quick stats readout -->
        </StackPanel>
      </ScrollViewer>
    </Grid>
  </DockPanel>
</Page>
```

### Style Alignment

- Uses existing `AppStyles.xaml` for button, textbox, and DataGrid styles.
- AvalonEdit themed via `ICSharpCode.AvalonEdit.Highlighting` — dark theme if `ThemeService.IsDarkMode`.
- ScottPlot uses `ScottPlot.Style.Seaborn` for clean chart aesthetics.
- Parameter sidebar uses the same form layout pattern as `SettingsPage.xaml`.

---

## 7. Test Plan

### Project: `MarketDataCollector.QuantScript.Tests`

All tests use xUnit + FluentAssertions. Mocking via NSubstitute.

#### 7.1 PriceSeries Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `Ctor_FromBars_MapsAllFields` | Close, Open, High, Low, Volume, Dates populated correctly |
| 2 | `Ctor_EmptyBars_CreatesEmptySeries` | Count == 0, no exceptions |
| 3 | `Slice_ValidRange_ReturnsSubset` | Only bars within date range |
| 4 | `Slice_OutOfRange_ReturnsEmpty` | Dates beyond series range → empty |
| 5 | `Returns_ComputesSimpleReturns` | `(C[i]-C[i-1])/C[i-1]` for known data |
| 6 | `LogReturns_ComputesCorrectly` | `ln(C[i]/C[i-1])` for known data |
| 7 | `ToQuotes_ProducesSkenderCompatible` | Result type is `IReadOnlyList<IQuote>`, count matches |

#### 7.2 ReturnSeries Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 8 | `AnnualisedReturn_KnownValues` | Manual calculation matches |
| 9 | `AnnualisedVolatility_KnownValues` | Manual calculation matches |
| 10 | `SharpeRatio_ZeroVol_ReturnsZero` | Edge case: no variance |
| 11 | `MaxDrawdown_MonotonicallyIncreasing_ReturnsZero` | No drawdown in rising series |
| 12 | `MaxDrawdown_KnownDrawdown_Correct` | Known peak-to-trough scenario |

#### 7.3 StatisticsEngine Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 13 | `Compute_FullStatistics_AllFieldsPopulated` | No NaN, no default values |
| 14 | `SortinoRatio_NoDownside_ReturnsPositiveInfinity` | Edge case |
| 15 | `CalmarRatio_ZeroDrawdown_Clamped` | Does not divide by zero |
| 16 | `Correlation_PerfectPositive_ReturnsOne` | Two identical series → 1.0 |
| 17 | `Correlation_PerfectNegative_ReturnsMinusOne` | Negated series → -1.0 |
| 18 | `RollingBeta_MarketVsItself_ReturnsOne` | Beta of market to itself = 1.0 |

#### 7.4 RoslynScriptCompiler Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 19 | `Compile_ValidScript_ReturnsSuccess` | `CompilationResult.Success == true` |
| 20 | `Compile_SyntaxError_ReturnsDiagnostics` | Error message, line number present |
| 21 | `Compile_AccessGlobals_BindsCorrectly` | Script can reference `Data`, `Plot`, `Stats` |
| 22 | `ExtractParameters_SingleParam_Extracted` | Name, type, default, min/max parsed |
| 23 | `ExtractParameters_NoParams_EmptyList` | No crash on plain scripts |
| 24 | `Compile_WithSkenderImport_Resolves` | Script using `GetSma` compiles without errors |

#### 7.5 ScriptRunner Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 25 | `RunAsync_SimpleScript_ReturnsResult` | Return value captured |
| 26 | `RunAsync_Exception_CapturedInResult` | `Success == false`, error message present |
| 27 | `RunAsync_Cancellation_ThrowsOperationCanceled` | Token respected |
| 28 | `RunAsync_Timeout_CancelsExecution` | Long-running script cancelled after timeout |
| 29 | `RunAsync_PlotCalls_DrainedInResult` | `result.Plots.Count > 0` |

#### 7.6 PlotQueue Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 30 | `Line_EnqueuesRequest` | Queue count increments |
| 31 | `DrainAll_ReturnsAllAndClears` | All items returned, queue empty after |
| 32 | `ConcurrentEnqueue_ThreadSafe` | Parallel writes, all items present in drain |

#### 7.7 TechnicalSeriesExtensions Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 33 | `SMA_KnownData_MatchesExpected` | SMA(3) on [1,2,3,4,5] → [null, null, 2, 3, 4] |
| 34 | `RSI_ReturnsCorrectCount` | Output length == input length |
| 35 | `BollingerBands_ReturnsTriple` | Upper > Middle > Lower for positive data |

#### 7.8 QuantDataContext Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 36 | `GetPricesAsync_DelegatesToStore` | Mock `HistoricalDataQueryService` called once |
| 37 | `GetPricesAsync_CachesResult` | Second call same params → no store call |
| 38 | `GetReturnsAsync_ComputesFromPrices` | Return series computed from fetched prices |

#### 7.9 Integration-Level Tests

| # | Test Name | What It Verifies |
|---|---|---|
| 39 | `EndToEnd_DataAnalysisScript_Succeeds` | Full compile → run → plot results pipeline |
| 40 | `EndToEnd_BacktestScript_ProducesMetrics` | Strategy compiled in-script, backtest executes |

---

## 8. Implementation Checklist

### Phase 1: Foundation (3–4 days)

- [ ] Create `src/MarketDataCollector.QuantScript/MarketDataCollector.QuantScript.csproj`
  - Target: `net9.0`
  - References: `Backtesting`, `Backtesting.Sdk`, `Application`, `Storage`, `Contracts`, `ProviderSdk`
- [ ] Add `Microsoft.CodeAnalysis.CSharp.Scripting` v5.0.0 to `Directory.Packages.props`
- [ ] Add `AvalonEditHighlightingThemes` or `AvalonEdit` (latest stable) to `Directory.Packages.props`
- [ ] Add `ScottPlot.WPF` (latest v5.x) to `Directory.Packages.props`
- [ ] Implement `PriceSeries` and `ReturnSeries` (§3.1)
- [ ] Implement `ScriptParamAttribute` and `ScriptParameterInfo` (§3.8)
- [ ] Implement `QuantScriptOptions` (§3.9)
- [ ] Add `[InternalsVisibleTo("MarketDataCollector.QuantScript")]` to `Backtesting.csproj` (for `BacktestMetricsEngine`)
- [ ] Create test project `tests/MarketDataCollector.QuantScript.Tests/`
- [ ] Write PriceSeries + ReturnSeries tests (tests 1–12)

### Phase 2: Compilation & Execution (3–4 days)

- [ ] Implement `IQuantScriptCompiler` and `RoslynScriptCompiler` (§3.3, §4.2)
- [ ] Implement `IScriptRunner` and `ScriptRunner` (§3.4, §4.3)
- [ ] Implement `PlotQueue` and `PlotRequest` (§3.5)
- [ ] Implement `QuantScriptGlobals`, `DataProxy`, `BacktestProxy` (§3.7)
- [ ] Write compiler + runner + PlotQueue tests (tests 19–32)

### Phase 3: Data & Statistics (2–3 days)

- [ ] Implement `IQuantDataContext` and `QuantDataContext` (§3.2, §4.1)
- [ ] Implement `StatisticsEngine`, `PortfolioStatistics`, `CorrelationMatrix` (§3.6, §4.5)
- [ ] Implement `TechnicalSeriesExtensions` (§3.10)
- [ ] Write statistics + data context + indicator tests (tests 13–18, 33–38)

### Phase 4: WPF Integration (3–4 days)

- [ ] Add `AvalonEdit` and `ScottPlot.WPF` `<PackageReference>` to `MarketDataCollector.Wpf.csproj`
- [ ] Add `MarketDataCollector.QuantScript` project reference to `MarketDataCollector.Wpf.csproj`
- [ ] Create `QuantScriptModels.cs` in `Models/` (ScriptParameterViewModel, etc.)
- [ ] Create `QuantScriptViewModel.cs` in `ViewModels/` (§4.7)
- [ ] Create `QuantScriptPage.xaml` + `.xaml.cs` in `Views/` (§6)
- [ ] Register page in `Pages.cs` navigation enum
- [ ] Register page in `NavigationService` and `MainPage.xaml` sidebar
- [ ] Register DI services in WPF `App.xaml.cs`
- [ ] Add default script template (momentum crossover example)

### Phase 5: Script Persistence & Polish (1–2 days)

- [ ] Implement save/load `.csx` files to `ScriptsDirectory`
- [ ] Implement recent scripts tracking
- [ ] Add "New Script" template with commented example
- [ ] Dark theme support for AvalonEdit (detect from `ThemeService`)
- [ ] Add `QuantScript` section to `config/appsettings.sample.json`

### Phase 6: End-to-End Testing & Docs (1–2 days)

- [ ] Write end-to-end integration tests (tests 39–40)
- [ ] Add page to WPF README
- [ ] Update `CLAUDE.md` with new project entry
- [ ] Add navigation menu icon and tooltip
- [ ] Smoke test: load SPY data, compute SMA, plot, run simple backtest

### Total Estimated Effort: ~15–19 working days (XL)

---

## 9. Open Questions & Risks

| # | Question / Risk | Impact | Mitigation |
|---|---|---|---|
| 1 | **Roslyn sandboxing** — user scripts can call `System.IO.File.Delete`, `Process.Start`, etc. | High — security risk for local data | Phase 1: document trust model (scripts are user-authored, like Excel macros). Phase 2+: investigate `AppDomain` isolation or assembly-level deny lists via Roslyn `MetadataReferenceResolver` filtering. |
| 2 | **ScottPlot v5 WPF control stability** | Medium — ScottPlot 5 is relatively new | Pin to a known-good version; fallback to OxyPlot.Wpf if ScottPlot proves problematic. Both are MIT-licensed. |
| 3 | **AvalonEdit C# IntelliSense** | Low — nice-to-have, not required for v1 | AvalonEdit has no built-in Roslyn completion. Defer to v2; could use `RoslynPad.Editor` if demand is high. |
| 4 | **Efficient Frontier / Portfolio Optimisation** | Low — complex maths, limited audience | Defer entirely. If needed later, add `MathNet.Numerics` for matrix operations and a `PortfolioOptimiser` class. |
| 5 | **`BacktestMetricsEngine` is internal** | Medium — compile error if not addressed | Add `[InternalsVisibleTo]` attribute to Backtesting project in Phase 1. Alternatively, make the class public. |
| 6 | **Roslyn cold-start latency** | Medium — first compilation may take 2–5 seconds | Show a "Compiling..." spinner. Consider pre-warming the Roslyn workspace on page load. |
| 7 | **Memory pressure from large datasets** | Low — PriceSeries caches bars in memory | For daily OHLCV bars, cap cache at ~50 symbols × 10 years ≈ ~2,500 bars per symbol × 50 ≈ ~125K bars total. Reduce the cap further for intraday data. Add `GC.Collect` after script completion if needed. |
| 8 | **Roslyn cold-start and async script entry point** | Low | Roslyn `CSharpScript` natively supports `await` at top-level in scripts; `RunAsync` on the `Script` object drives the `async` state machine. No wrapper or workaround needed. |

---

*Generated by mdc-blueprint skill. This document is code-ready: all interfaces can be copy-pasted into `.cs` files and will compile against the existing MDC dependency graph.*
