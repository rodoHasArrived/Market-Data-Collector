using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for generating schemas and data dictionaries (#37/72 - P0 High).
/// Auto-generates comprehensive documentation for all event types.
/// </summary>
public sealed class SchemaService : ISchemaService
{
    private static SchemaService? _instance;
    private static readonly object _lock = new();

    private readonly string _schemasPath;
    private DataDictionary? _dataDictionary;

    public static SchemaService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SchemaService();
                }
            }
            return _instance;
        }
    }

    private SchemaService()
    {
        _schemasPath = Path.Combine(AppContext.BaseDirectory, "_catalog", "schemas");
    }

    /// <summary>
    /// Gets the complete data dictionary with all schemas.
    /// </summary>
    public async Task<DataDictionary> GetDataDictionaryAsync(CancellationToken cancellationToken = default)
    {
        if (_dataDictionary != null)
        {
            return _dataDictionary;
        }

        _dataDictionary = await LoadOrCreateDataDictionaryAsync(cancellationToken);
        return _dataDictionary;
    }

    /// <summary>
    /// Gets schema for a specific event type.
    /// </summary>
    public async Task<EventSchema?> GetSchemaAsync(string eventType, CancellationToken cancellationToken = default)
    {
        var dictionary = await GetDataDictionaryAsync(cancellationToken);
        return dictionary.Schemas.TryGetValue(eventType, out var schema) ? schema : null;
    }

    /// <summary>
    /// Generates the data dictionary and saves it to disk.
    /// </summary>
    public async Task<DataDictionary> GenerateDataDictionaryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dictionary = new DataDictionary
        {
            Version = "2.0",
            GeneratedAt = DateTime.UtcNow,
            Schemas = new Dictionary<string, EventSchema>
            {
                ["Trade"] = CreateTradeSchema(),
                ["Quote"] = CreateQuoteSchema(),
                ["BboQuote"] = CreateBboQuoteSchema(),
                ["L2Depth"] = CreateL2DepthSchema(),
                ["Bar"] = CreateBarSchema()
            },
            ExchangeCodes = GetExchangeCodes(),
            TradeConditions = GetTradeConditions(),
            QuoteConditions = GetQuoteConditions()
        };

        _dataDictionary = dictionary;
        await SaveDataDictionaryAsync(dictionary, cancellationToken);

        DictionaryGenerated?.Invoke(this, new DataDictionaryEventArgs { Dictionary = dictionary });

        return dictionary;
    }

    /// <summary>
    /// Exports the data dictionary in different formats.
    /// </summary>
    public async Task<string> ExportDataDictionaryAsync(string format, string? outputPath = null, CancellationToken cancellationToken = default)
    {
        var dictionary = await GetDataDictionaryAsync(cancellationToken);
        var output = format.ToLower() switch
        {
            "json" => ExportAsJson(dictionary),
            "markdown" or "md" => ExportAsMarkdown(dictionary),
            "csv" => ExportAsCsv(dictionary),
            _ => ExportAsJson(dictionary)
        };

        if (!string.IsNullOrEmpty(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, output, cancellationToken);
        }

        return output;
    }

    /// <summary>
    /// Generates a markdown data dictionary document.
    /// </summary>
    public async Task<string> GenerateMarkdownDocumentationAsync(CancellationToken cancellationToken = default)
    {
        var dictionary = await GetDataDictionaryAsync(cancellationToken);
        return ExportAsMarkdown(dictionary);
    }

    private async Task<DataDictionary> LoadOrCreateDataDictionaryAsync(CancellationToken cancellationToken)
    {
        EnsureSchemasPathExists();
        var dictionaryPath = Path.Combine(_schemasPath, "data_dictionary.json");

        if (File.Exists(dictionaryPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(dictionaryPath, cancellationToken);
                var dictionary = JsonSerializer.Deserialize<DataDictionary>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (dictionary != null)
                {
                    return dictionary;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Failed to load data dictionary", ex);
            }
        }

        return await GenerateDataDictionaryAsync(cancellationToken);
    }

    private async Task SaveDataDictionaryAsync(DataDictionary dictionary, CancellationToken cancellationToken)
    {
        EnsureSchemasPathExists();
        var dictionaryPath = Path.Combine(_schemasPath, "data_dictionary.json");

        var json = JsonSerializer.Serialize(dictionary, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(dictionaryPath, json, cancellationToken);

        // Also save markdown version
        var markdownPath = Path.Combine(_schemasPath, "DATA_DICTIONARY.md");
        await File.WriteAllTextAsync(markdownPath, ExportAsMarkdown(dictionary), cancellationToken);
    }

    private void EnsureSchemasPathExists()
    {
        if (!Directory.Exists(_schemasPath))
        {
            Directory.CreateDirectory(_schemasPath);
        }
    }

    private static EventSchema CreateTradeSchema()
    {
        return new EventSchema
        {
            Name = "Trade",
            Version = "2.0.0",
            Description = "Represents a single trade execution event with price, size, and exchange information.",
            IntroducedAt = new DateTime(2025, 1, 1),
            Fields = new[]
            {
                new SchemaField
                {
                    Name = "Timestamp",
                    Type = "datetime",
                    Description = "Event timestamp in UTC with nanosecond precision",
                    Format = "ISO8601",
                    Example = "2026-01-03T14:30:00.123456789Z"
                },
                new SchemaField
                {
                    Name = "Symbol",
                    Type = "string",
                    Description = "Ticker symbol",
                    Example = "AAPL"
                },
                new SchemaField
                {
                    Name = "Price",
                    Type = "decimal",
                    Description = "Trade price",
                    Format = "decimal(18,8)",
                    ValidRange = new FieldValidRange { Min = 0.0001m }
                },
                new SchemaField
                {
                    Name = "Size",
                    Type = "int64",
                    Description = "Trade size in shares",
                    ValidRange = new FieldValidRange { Min = 1L }
                },
                new SchemaField
                {
                    Name = "Side",
                    Type = "enum",
                    Description = "Aggressor side of the trade",
                    EnumValues = new[] { "Buy", "Sell", "Unknown" },
                    Example = "Buy"
                },
                new SchemaField
                {
                    Name = "Exchange",
                    Type = "string",
                    Description = "Exchange code where trade occurred",
                    Example = "XNAS",
                    ExchangeSpecific = true
                },
                new SchemaField
                {
                    Name = "TradeId",
                    Type = "string",
                    Description = "Unique trade identifier from exchange",
                    Nullable = true,
                    Example = "T123456789"
                },
                new SchemaField
                {
                    Name = "Conditions",
                    Type = "string[]",
                    Description = "Trade condition codes (exchange-specific)",
                    Nullable = true,
                    Example = new[] { "@", "F" },
                    ExchangeSpecific = true
                },
                new SchemaField
                {
                    Name = "SequenceNumber",
                    Type = "int64",
                    Description = "Sequence number for ordering",
                    Nullable = true
                }
            },
            PrimaryKey = new[] { "Timestamp", "Symbol", "TradeId" },
            Indexes = new[] { new[] { "Symbol", "Timestamp" } },
            SampleRecord = new Dictionary<string, object>
            {
                ["Timestamp"] = "2026-01-03T14:30:00.123456789Z",
                ["Symbol"] = "AAPL",
                ["Price"] = 185.25,
                ["Size"] = 100,
                ["Side"] = "Buy",
                ["Exchange"] = "XNAS",
                ["TradeId"] = "T123456789",
                ["Conditions"] = new[] { "@" }
            }
        };
    }

    private static EventSchema CreateQuoteSchema()
    {
        return new EventSchema
        {
            Name = "Quote",
            Version = "2.0.0",
            Description = "Represents a quote update with bid/ask prices and sizes.",
            IntroducedAt = new DateTime(2025, 1, 1),
            Fields = new[]
            {
                new SchemaField
                {
                    Name = "Timestamp",
                    Type = "datetime",
                    Description = "Event timestamp in UTC",
                    Format = "ISO8601"
                },
                new SchemaField
                {
                    Name = "Symbol",
                    Type = "string",
                    Description = "Ticker symbol"
                },
                new SchemaField
                {
                    Name = "BidPrice",
                    Type = "decimal",
                    Description = "Best bid price",
                    Format = "decimal(18,8)",
                    Nullable = true
                },
                new SchemaField
                {
                    Name = "BidSize",
                    Type = "int64",
                    Description = "Size at best bid",
                    Nullable = true
                },
                new SchemaField
                {
                    Name = "AskPrice",
                    Type = "decimal",
                    Description = "Best ask price",
                    Format = "decimal(18,8)",
                    Nullable = true
                },
                new SchemaField
                {
                    Name = "AskSize",
                    Type = "int64",
                    Description = "Size at best ask",
                    Nullable = true
                },
                new SchemaField
                {
                    Name = "BidExchange",
                    Type = "string",
                    Description = "Exchange code for best bid",
                    Nullable = true,
                    ExchangeSpecific = true
                },
                new SchemaField
                {
                    Name = "AskExchange",
                    Type = "string",
                    Description = "Exchange code for best ask",
                    Nullable = true,
                    ExchangeSpecific = true
                },
                new SchemaField
                {
                    Name = "Conditions",
                    Type = "string[]",
                    Description = "Quote condition codes",
                    Nullable = true,
                    ExchangeSpecific = true
                }
            },
            PrimaryKey = new[] { "Timestamp", "Symbol" },
            Indexes = new[] { new[] { "Symbol", "Timestamp" } }
        };
    }

    private static EventSchema CreateBboQuoteSchema()
    {
        return new EventSchema
        {
            Name = "BboQuote",
            Version = "2.0.0",
            Description = "Best Bid and Offer (NBBO) quote with national market system data.",
            IntroducedAt = new DateTime(2025, 1, 1),
            Fields = new[]
            {
                new SchemaField
                {
                    Name = "Timestamp",
                    Type = "datetime",
                    Description = "Event timestamp in UTC",
                    Format = "ISO8601"
                },
                new SchemaField
                {
                    Name = "Symbol",
                    Type = "string",
                    Description = "Ticker symbol"
                },
                new SchemaField
                {
                    Name = "NbboBidPrice",
                    Type = "decimal",
                    Description = "National best bid price",
                    Format = "decimal(18,8)"
                },
                new SchemaField
                {
                    Name = "NbboBidSize",
                    Type = "int64",
                    Description = "Size at national best bid"
                },
                new SchemaField
                {
                    Name = "NbboAskPrice",
                    Type = "decimal",
                    Description = "National best ask price",
                    Format = "decimal(18,8)"
                },
                new SchemaField
                {
                    Name = "NbboAskSize",
                    Type = "int64",
                    Description = "Size at national best ask"
                },
                new SchemaField
                {
                    Name = "MidPrice",
                    Type = "decimal",
                    Description = "Calculated mid price ((bid+ask)/2)",
                    Format = "decimal(18,8)"
                },
                new SchemaField
                {
                    Name = "Spread",
                    Type = "decimal",
                    Description = "Bid-ask spread",
                    Format = "decimal(18,8)"
                }
            },
            PrimaryKey = new[] { "Timestamp", "Symbol" }
        };
    }

    private static EventSchema CreateL2DepthSchema()
    {
        return new EventSchema
        {
            Name = "L2Depth",
            Version = "2.0.0",
            Description = "Level 2 market depth snapshot with multiple price levels.",
            IntroducedAt = new DateTime(2025, 1, 1),
            Fields = new[]
            {
                new SchemaField
                {
                    Name = "Timestamp",
                    Type = "datetime",
                    Description = "Snapshot timestamp in UTC",
                    Format = "ISO8601"
                },
                new SchemaField
                {
                    Name = "Symbol",
                    Type = "string",
                    Description = "Ticker symbol"
                },
                new SchemaField
                {
                    Name = "Bids",
                    Type = "array",
                    Description = "Array of bid price levels, ordered by price descending",
                    Notes = "Each element contains: {Price, Size, NumOrders, Exchange}"
                },
                new SchemaField
                {
                    Name = "Asks",
                    Type = "array",
                    Description = "Array of ask price levels, ordered by price ascending",
                    Notes = "Each element contains: {Price, Size, NumOrders, Exchange}"
                },
                new SchemaField
                {
                    Name = "Depth",
                    Type = "int32",
                    Description = "Number of levels on each side",
                    ValidRange = new FieldValidRange { Min = 1, Max = 50 }
                },
                new SchemaField
                {
                    Name = "Exchange",
                    Type = "string",
                    Description = "Exchange code for this depth data",
                    ExchangeSpecific = true
                }
            },
            PrimaryKey = new[] { "Timestamp", "Symbol", "Exchange" }
        };
    }

    private static EventSchema CreateBarSchema()
    {
        return new EventSchema
        {
            Name = "Bar",
            Version = "2.0.0",
            Description = "OHLCV bar/candlestick data for a time interval.",
            IntroducedAt = new DateTime(2025, 1, 1),
            Fields = new[]
            {
                new SchemaField
                {
                    Name = "Timestamp",
                    Type = "datetime",
                    Description = "Bar start time in UTC",
                    Format = "ISO8601"
                },
                new SchemaField
                {
                    Name = "Symbol",
                    Type = "string",
                    Description = "Ticker symbol"
                },
                new SchemaField
                {
                    Name = "Open",
                    Type = "decimal",
                    Description = "Opening price",
                    Format = "decimal(18,8)"
                },
                new SchemaField
                {
                    Name = "High",
                    Type = "decimal",
                    Description = "Highest price during interval",
                    Format = "decimal(18,8)"
                },
                new SchemaField
                {
                    Name = "Low",
                    Type = "decimal",
                    Description = "Lowest price during interval",
                    Format = "decimal(18,8)"
                },
                new SchemaField
                {
                    Name = "Close",
                    Type = "decimal",
                    Description = "Closing price",
                    Format = "decimal(18,8)"
                },
                new SchemaField
                {
                    Name = "Volume",
                    Type = "int64",
                    Description = "Total volume during interval"
                },
                new SchemaField
                {
                    Name = "VWAP",
                    Type = "decimal",
                    Description = "Volume-weighted average price",
                    Format = "decimal(18,8)",
                    Nullable = true
                },
                new SchemaField
                {
                    Name = "TradeCount",
                    Type = "int32",
                    Description = "Number of trades during interval",
                    Nullable = true
                },
                new SchemaField
                {
                    Name = "Resolution",
                    Type = "string",
                    Description = "Bar resolution",
                    EnumValues = new[] { "Tick", "Second", "Minute", "Hour", "Daily" }
                }
            },
            PrimaryKey = new[] { "Timestamp", "Symbol", "Resolution" }
        };
    }

    private static Dictionary<string, string> GetExchangeCodes()
    {
        return new Dictionary<string, string>
        {
            ["XNAS"] = "NASDAQ Stock Market",
            ["XNYS"] = "New York Stock Exchange",
            ["ARCX"] = "NYSE Arca",
            ["XASE"] = "NYSE American (AMEX)",
            ["BATS"] = "CBOE BZX Exchange",
            ["BATY"] = "CBOE BYX Exchange",
            ["EDGA"] = "CBOE EDGA Exchange",
            ["EDGX"] = "CBOE EDGX Exchange",
            ["IEXG"] = "IEX Exchange",
            ["XCHI"] = "Chicago Stock Exchange",
            ["XPHL"] = "NASDAQ OMX PHLX",
            ["XBOS"] = "NASDAQ OMX BX",
            ["MEMX"] = "Members Exchange",
            ["LTSE"] = "Long-Term Stock Exchange"
        };
    }

    private static Dictionary<string, string> GetTradeConditions()
    {
        return new Dictionary<string, string>
        {
            ["@"] = "Regular Sale",
            ["A"] = "Acquisition",
            ["B"] = "Bunched Trade",
            ["C"] = "Cash Sale",
            ["D"] = "Distribution",
            ["E"] = "Automatic Execution",
            ["F"] = "Intermarket Sweep",
            ["G"] = "Opening/Reopening Trade Detail",
            ["H"] = "Intraday Trade Detail",
            ["I"] = "CAP Election Trade",
            ["K"] = "Rule 155 Trade (NYSE AMEX)",
            ["L"] = "Sold Last",
            ["M"] = "Market Center Close Price",
            ["N"] = "Next Day",
            ["O"] = "Opening Trade Detail",
            ["P"] = "Prior Reference Price",
            ["Q"] = "Market Center Open Price",
            ["R"] = "Seller",
            ["S"] = "Split Trade",
            ["T"] = "Form T",
            ["U"] = "Extended Trading Hours (Sold Out of Sequence)",
            ["V"] = "Contingent Trade",
            ["W"] = "Average Price Trade",
            ["X"] = "Cross Trade",
            ["Y"] = "Yellow Flag Regular Trade",
            ["Z"] = "Sold (Out of Sequence)",
            ["1"] = "Stopped Stock (Regular Trade)",
            ["4"] = "Derivatively Priced",
            ["5"] = "Re-Opening Prints",
            ["6"] = "Closing Prints",
            ["7"] = "Qualified Contingent Trade",
            ["8"] = "Placeholder For 611 Exempt",
            ["9"] = "Corrected Consolidated Close"
        };
    }

    private static Dictionary<string, string> GetQuoteConditions()
    {
        return new Dictionary<string, string>
        {
            ["A"] = "Slow Quote Offer Side",
            ["B"] = "Slow Quote Bid Side",
            ["C"] = "Closing",
            ["D"] = "News Dissemination",
            ["E"] = "Slow Quote LRP Bid Side",
            ["F"] = "Slow Quote LRP Offer Side",
            ["G"] = "Slow Quote Bid and Offer Side",
            ["H"] = "Slow Quote LRP Bid and Offer Side",
            ["I"] = "Order Imbalance",
            ["J"] = "Due to Related Security - News Dissemination",
            ["K"] = "Due to Related Security - News Pending",
            ["L"] = "Additional Information",
            ["M"] = "Non-Firm Quote",
            ["N"] = "News Pending",
            ["O"] = "Opening",
            ["P"] = "Additional Information - Due to Related Security",
            ["Q"] = "Regular",
            ["R"] = "Rotation",
            ["S"] = "Suspended Trading",
            ["T"] = "Trading Range Indication",
            ["U"] = "Slow Quote On Bid And Offer (No Firm Quote)",
            ["V"] = "Slow Quote Set Slow List",
            ["W"] = "Slow Quote LRP Bid Side And Offer Side",
            ["X"] = "Closed",
            ["Y"] = "Slow Quote Demand Side",
            ["Z"] = "Slow Quote No Reason",
            ["0"] = "No Special Condition",
            ["1"] = "Manual/Slow Quote",
            ["2"] = "Fast Trading",
            ["3"] = "Rotation"
        };
    }

    private static string ExportAsJson(DataDictionary dictionary)
    {
        return JsonSerializer.Serialize(dictionary, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string ExportAsMarkdown(DataDictionary dictionary)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Market Data Collector - Data Dictionary");
        sb.AppendLine();
        sb.AppendLine($"**Version:** {dictionary.Version}");
        sb.AppendLine($"**Generated:** {dictionary.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Table of Contents");
        sb.AppendLine();
        foreach (var schema in dictionary.Schemas.Values.OrderBy(s => s.Name))
        {
            sb.AppendLine($"- [{schema.Name} Event](#-{schema.Name.ToLower()}-event-schema)");
        }
        sb.AppendLine("- [Exchange Codes](#exchange-codes)");
        sb.AppendLine("- [Trade Conditions](#trade-conditions)");
        sb.AppendLine("- [Quote Conditions](#quote-conditions)");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Schemas
        foreach (var schema in dictionary.Schemas.Values.OrderBy(s => s.Name))
        {
            sb.AppendLine($"## {schema.Name} Event Schema");
            sb.AppendLine();
            sb.AppendLine($"**Version:** {schema.Version}");
            sb.AppendLine();
            sb.AppendLine(schema.Description);
            sb.AppendLine();
            sb.AppendLine("### Fields");
            sb.AppendLine();
            sb.AppendLine("| Field | Type | Description | Nullable | Example |");
            sb.AppendLine("|-------|------|-------------|----------|---------|");

            foreach (var field in schema.Fields)
            {
                var example = field.Example?.ToString() ?? (field.EnumValues != null ? string.Join(", ", field.EnumValues) : "");
                var nullable = field.Nullable ? "Yes" : "No";
                sb.AppendLine($"| {field.Name} | {field.Type} | {field.Description ?? ""} | {nullable} | {example} |");
            }

            sb.AppendLine();

            if (schema.PrimaryKey != null && schema.PrimaryKey.Length > 0)
            {
                sb.AppendLine($"**Primary Key:** {string.Join(", ", schema.PrimaryKey)}");
                sb.AppendLine();
            }

            if (schema.SampleRecord != null)
            {
                sb.AppendLine("### Sample Record");
                sb.AppendLine();
                sb.AppendLine("```json");
                sb.AppendLine(JsonSerializer.Serialize(schema.SampleRecord, new JsonSerializerOptions { WriteIndented = true }));
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Exchange Codes
        if (dictionary.ExchangeCodes != null)
        {
            sb.AppendLine("## Exchange Codes");
            sb.AppendLine();
            sb.AppendLine("| Code | Description |");
            sb.AppendLine("|------|-------------|");
            foreach (var (code, description) in dictionary.ExchangeCodes.OrderBy(x => x.Key))
            {
                sb.AppendLine($"| {code} | {description} |");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Trade Conditions
        if (dictionary.TradeConditions != null)
        {
            sb.AppendLine("## Trade Conditions");
            sb.AppendLine();
            sb.AppendLine("| Code | Description |");
            sb.AppendLine("|------|-------------|");
            foreach (var (code, description) in dictionary.TradeConditions.OrderBy(x => x.Key))
            {
                sb.AppendLine($"| {code} | {description} |");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Quote Conditions
        if (dictionary.QuoteConditions != null)
        {
            sb.AppendLine("## Quote Conditions");
            sb.AppendLine();
            sb.AppendLine("| Code | Description |");
            sb.AppendLine("|------|-------------|");
            foreach (var (code, description) in dictionary.QuoteConditions.OrderBy(x => x.Key))
            {
                sb.AppendLine($"| {code} | {description} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExportAsCsv(DataDictionary dictionary)
    {
        var sb = new StringBuilder();

        sb.AppendLine("EventType,FieldName,Type,Description,Nullable,Format,Example");

        foreach (var schema in dictionary.Schemas.Values.OrderBy(s => s.Name))
        {
            foreach (var field in schema.Fields)
            {
                var example = field.Example?.ToString()?.Replace("\"", "\"\"") ?? "";
                var description = field.Description?.Replace("\"", "\"\"") ?? "";
                sb.AppendLine($"\"{schema.Name}\",\"{field.Name}\",\"{field.Type}\",\"{description}\",\"{field.Nullable}\",\"{field.Format ?? ""}\",\"{example}\"");
            }
        }

        return sb.ToString();
    }

    // Events
    public event EventHandler<DataDictionaryEventArgs>? DictionaryGenerated;
}

/// <summary>
/// Event args for data dictionary events.
/// </summary>
public class DataDictionaryEventArgs : EventArgs
{
    public DataDictionary? Dictionary { get; set; }
}
