using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using MarketDataCollector.Application.Logging;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Services.CandleBuilding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Serilog;

namespace MarketDataCollector.Services.Analytics;

/// <summary>
/// Analytics engine for processing market data with embedded C# scripts.
/// Inspired by StockSharp Hydra's analytics feature.
///
/// Allows users to:
/// - Write custom C# analysis code
/// - Access stored market data (trades, candles, depth)
/// - Perform calculations and generate reports
/// - Visualize results
/// </summary>
public sealed class AnalyticsEngine : IDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<AnalyticsEngine>();
    private readonly ConcurrentDictionary<string, CompiledScript> _compiledScripts = new();
    private readonly AnalyticsContext _context;
    private bool _disposed;

    public AnalyticsEngine()
    {
        _context = new AnalyticsContext();
    }

    /// <summary>
    /// Execute a C# analytics script.
    /// </summary>
    /// <param name="script">C# code to execute.</param>
    /// <param name="data">Market data to analyze.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Script execution result.</returns>
    public async Task<AnalyticsResult> ExecuteAsync(
        string script,
        AnalyticsData data,
        CancellationToken ct = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _log.Debug("Executing analytics script ({Length} chars)", script.Length);

            // Get or compile script
            var compiled = GetOrCompileScript(script);

            // Execute with data context
            _context.Data = data;
            _context.Output.Clear();

            var result = await Task.Run(() => compiled.Execute(_context), ct);

            return new AnalyticsResult
            {
                Success = true,
                Output = _context.Output.ToList(),
                Metrics = _context.Metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Duration = DateTimeOffset.UtcNow - startTime,
                ReturnValue = result
            };
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Analytics script execution failed");

            return new AnalyticsResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTimeOffset.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Compile a script without executing it.
    /// </summary>
    public CompilationResult Compile(string script)
    {
        try
        {
            var compiled = CompileScript(script);
            return new CompilationResult { Success = true };
        }
        catch (Exception ex)
        {
            return new CompilationResult
            {
                Success = false,
                Errors = new[] { ex.Message }
            };
        }
    }

    /// <summary>
    /// Get built-in analysis functions.
    /// </summary>
    public IReadOnlyList<AnalyticsFunction> GetBuiltInFunctions()
    {
        return new List<AnalyticsFunction>
        {
            new("CalculateVwap", "Calculate VWAP from trades", new[] { "trades" }),
            new("CalculateReturns", "Calculate returns from candles", new[] { "candles" }),
            new("CalculateVolatility", "Calculate volatility (std dev of returns)", new[] { "candles", "period" }),
            new("CalculateDrawdown", "Calculate maximum drawdown", new[] { "candles" }),
            new("FindPatterns", "Find candlestick patterns", new[] { "candles", "patternType" }),
            new("CalculateCorrelation", "Calculate correlation between two series", new[] { "series1", "series2" }),
            new("CalculateSharpe", "Calculate Sharpe ratio", new[] { "returns", "riskFreeRate" }),
            new("BuildVolumeProfile", "Build volume profile from trades", new[] { "trades", "tickSize" }),
            new("AnalyzeOrderFlow", "Analyze order flow imbalance", new[] { "trades" }),
            new("DetectAnomalies", "Detect price/volume anomalies", new[] { "candles", "threshold" })
        };
    }

    private CompiledScript GetOrCompileScript(string script)
    {
        var hash = script.GetHashCode().ToString();

        return _compiledScripts.GetOrAdd(hash, _ => CompileScript(script));
    }

    private CompiledScript CompileScript(string script)
    {
        // Wrap user script in a class
        var fullCode = WrapScript(script);

        var syntaxTree = CSharpSyntaxTree.ParseText(fullCode);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Trade).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location)
        };

        var compilation = CSharpCompilation.Create(
            $"AnalyticsScript_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToArray();

            throw new InvalidOperationException(
                $"Script compilation failed:\n{string.Join("\n", errors)}");
        }

        ms.Seek(0, SeekOrigin.Begin);

        var context = new AssemblyLoadContext(null, isCollectible: true);
        var assembly = context.LoadFromStream(ms);

        var type = assembly.GetType("MarketDataCollector.Analytics.UserScript");
        var method = type!.GetMethod("Execute");

        return new CompiledScript(assembly, context, type, method!);
    }

    private static string WrapScript(string userCode)
    {
        return $@"
using System;
using System.Collections.Generic;
using System.Linq;
using MarketDataCollector.Domain.Models;
using MarketDataCollector.Domain.Events;
using MarketDataCollector.Services.CandleBuilding;
using MarketDataCollector.Services.Analytics;

namespace MarketDataCollector.Analytics
{{
    public static class UserScript
    {{
        public static object Execute(AnalyticsContext ctx)
        {{
            var data = ctx.Data;
            var trades = data.Trades;
            var candles = data.Candles;
            var quotes = data.Quotes;
            var depth = data.DepthSnapshots;

            // Helper functions
            Action<string> print = msg => ctx.Output.Add(msg);
            Action<string, object> setMetric = (name, value) => ctx.Metrics[name] = value;

            // Built-in analytics functions
            Func<IEnumerable<Trade>, decimal> vwap = t => {{
                var totalValue = t.Sum(x => x.Price * x.Size);
                var totalVolume = t.Sum(x => x.Size);
                return totalVolume > 0 ? totalValue / totalVolume : 0;
            }};

            Func<IEnumerable<Candle>, IEnumerable<decimal>> returns = c => {{
                var list = c.ToList();
                var result = new List<decimal>();
                for (int i = 1; i < list.Count; i++)
                {{
                    if (list[i-1].Close > 0)
                        result.Add((list[i].Close - list[i-1].Close) / list[i-1].Close);
                }}
                return result;
            }};

            // User code
            {userCode}

            return null;
        }}
    }}
}}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var script in _compiledScripts.Values)
        {
            script.Dispose();
        }
        _compiledScripts.Clear();
    }
}

/// <summary>
/// Compiled script holder.
/// </summary>
internal sealed class CompiledScript : IDisposable
{
    private readonly Assembly _assembly;
    private readonly AssemblyLoadContext _context;
    private readonly Type _type;
    private readonly MethodInfo _method;

    public CompiledScript(Assembly assembly, AssemblyLoadContext context, Type type, MethodInfo method)
    {
        _assembly = assembly;
        _context = context;
        _type = type;
        _method = method;
    }

    public object? Execute(AnalyticsContext context)
    {
        return _method.Invoke(null, new object[] { context });
    }

    public void Dispose()
    {
        _context.Unload();
    }
}

/// <summary>
/// Context passed to analytics scripts.
/// </summary>
public sealed class AnalyticsContext
{
    /// <summary>Market data for analysis.</summary>
    public AnalyticsData Data { get; set; } = new();

    /// <summary>Output messages from script.</summary>
    public List<string> Output { get; } = new();

    /// <summary>Metrics calculated by script.</summary>
    public ConcurrentDictionary<string, object> Metrics { get; } = new();
}

/// <summary>
/// Market data passed to analytics scripts.
/// </summary>
public sealed class AnalyticsData
{
    public IReadOnlyList<Trade> Trades { get; init; } = Array.Empty<Trade>();
    public IReadOnlyList<Candle> Candles { get; init; } = Array.Empty<Candle>();
    public IReadOnlyList<BboQuotePayload> Quotes { get; init; } = Array.Empty<BboQuotePayload>();
    public IReadOnlyList<LOBSnapshot> DepthSnapshots { get; init; } = Array.Empty<LOBSnapshot>();
    public string Symbol { get; init; } = "";
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
}

/// <summary>
/// Result of analytics script execution.
/// </summary>
public sealed record AnalyticsResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> Output { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, object> Metrics { get; init; } = new Dictionary<string, object>();
    public TimeSpan Duration { get; init; }
    public object? ReturnValue { get; init; }
}

/// <summary>
/// Result of script compilation.
/// </summary>
public sealed record CompilationResult
{
    public bool Success { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Description of a built-in analytics function.
/// </summary>
public sealed record AnalyticsFunction(
    string Name,
    string Description,
    string[] Parameters
);
