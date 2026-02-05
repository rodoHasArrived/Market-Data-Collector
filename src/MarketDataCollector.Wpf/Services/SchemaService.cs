using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MarketDataCollector.Wpf.Services;

/// <summary>
/// Service for generating schemas and data dictionaries.
/// </summary>
public sealed class SchemaService
{
    private static readonly Lazy<SchemaService> _instance = new(() => new SchemaService());
    public static SchemaService Instance => _instance.Value;

    private readonly string _schemasPath;
    private DataDictionary? _dataDictionary;

    private SchemaService()
    {
        _schemasPath = Path.Combine(AppContext.BaseDirectory, "_catalog", "schemas");
    }

    public event EventHandler<DataDictionaryEventArgs>? DictionaryGenerated;

    public async Task<DataDictionary> GetDataDictionaryAsync()
    {
        if (_dataDictionary != null) return _dataDictionary;
        _dataDictionary = await LoadOrCreateDataDictionaryAsync();
        return _dataDictionary;
    }

    public async Task<EventSchema?> GetSchemaAsync(string eventType)
    {
        var dictionary = await GetDataDictionaryAsync();
        return dictionary.Schemas.TryGetValue(eventType, out var schema) ? schema : null;
    }

    public async Task<DataDictionary> GenerateDataDictionaryAsync()
    {
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
        await SaveDataDictionaryAsync(dictionary);
        DictionaryGenerated?.Invoke(this, new DataDictionaryEventArgs { Dictionary = dictionary });
        return dictionary;
    }

    public async Task<string> ExportDataDictionaryAsync(string format, string? outputPath = null)
    {
        var dictionary = await GetDataDictionaryAsync();
        var output = format.ToLower() switch
        {
            "json" => ExportAsJson(dictionary),
            "markdown" or "md" => ExportAsMarkdown(dictionary),
            "csv" => ExportAsCsv(dictionary),
            _ => ExportAsJson(dictionary)
        };

        if (!string.IsNullOrEmpty(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, output);
        }

        return output;
    }

    public async Task<string> GenerateMarkdownDocumentationAsync()
    {
        var dictionary = await GetDataDictionaryAsync();
        return ExportAsMarkdown(dictionary);
    }

    private async Task<DataDictionary> LoadOrCreateDataDictionaryAsync()
    {
        EnsureSchemasPathExists();
        var dictionaryPath = Path.Combine(_schemasPath, "data_dictionary.json");

        if (File.Exists(dictionaryPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(dictionaryPath);
                var dictionary = JsonSerializer.Deserialize<DataDictionary>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (dictionary != null) return dictionary;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load data dictionary: {ex.Message}");
            }
        }

        return await GenerateDataDictionaryAsync();
    }

    private async Task SaveDataDictionaryAsync(DataDictionary dictionary)
    {
        EnsureSchemasPathExists();
        var dictionaryPath = Path.Combine(_schemasPath, "data_dictionary.json");

        var json = JsonSerializer.Serialize(dictionary, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(dictionaryPath, json);

        var markdownPath = Path.Combine(_schemasPath, "DATA_DICTIONARY.md");
        await File.WriteAllTextAsync(markdownPath, ExportAsMarkdown(dictionary));
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
                new SchemaField { Name = "Timestamp", Type = "datetime", Description = "Event timestamp in UTC with nanosecond precision", Format = "ISO8601" },
                new SchemaField { Name = "Symbol", Type = "string", Description = "Ticker symbol" },
                new SchemaField { Name = "Price", Type = "decimal", Description = "Trade price", Format = "decimal(18,8)" },
                new SchemaField { Name = "Size", Type = "int64", Description = "Trade size in shares" },
                new SchemaField { Name = "Side", Type = "enum", Description = "Aggressor side of the trade", EnumValues = new[] { "Buy", "Sell", "Unknown" } },
                new SchemaField { Name = "Exchange", Type = "string", Description = "Exchange code where trade occurred", ExchangeSpecific = true },
                new SchemaField { Name = "TradeId", Type = "string", Description = "Unique trade identifier from exchange", Nullable = true },
                new SchemaField { Name = "Conditions", Type = "string[]", Description = "Trade condition codes", Nullable = true, ExchangeSpecific = true },
                new SchemaField { Name = "SequenceNumber", Type = "int64", Description = "Sequence number for ordering", Nullable = true }
            },
            PrimaryKey = new[] { "Timestamp", "Symbol", "TradeId" }
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
                new SchemaField { Name = "Timestamp", Type = "datetime", Description = "Event timestamp in UTC", Format = "ISO8601" },
                new SchemaField { Name = "Symbol", Type = "string", Description = "Ticker symbol" },
                new SchemaField { Name = "BidPrice", Type = "decimal", Description = "Best bid price", Nullable = true },
                new SchemaField { Name = "BidSize", Type = "int64", Description = "Size at best bid", Nullable = true },
                new SchemaField { Name = "AskPrice", Type = "decimal", Description = "Best ask price", Nullable = true },
                new SchemaField { Name = "AskSize", Type = "int64", Description = "Size at best ask", Nullable = true },
                new SchemaField { Name = "BidExchange", Type = "string", Description = "Exchange code for best bid", Nullable = true, ExchangeSpecific = true },
                new SchemaField { Name = "AskExchange", Type = "string", Description = "Exchange code for best ask", Nullable = true, ExchangeSpecific = true }
            },
            PrimaryKey = new[] { "Timestamp", "Symbol" }
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
                new SchemaField { Name = "Timestamp", Type = "datetime", Description = "Event timestamp in UTC", Format = "ISO8601" },
                new SchemaField { Name = "Symbol", Type = "string", Description = "Ticker symbol" },
                new SchemaField { Name = "NbboBidPrice", Type = "decimal", Description = "National best bid price" },
                new SchemaField { Name = "NbboBidSize", Type = "int64", Description = "Size at national best bid" },
                new SchemaField { Name = "NbboAskPrice", Type = "decimal", Description = "National best ask price" },
                new SchemaField { Name = "NbboAskSize", Type = "int64", Description = "Size at national best ask" },
                new SchemaField { Name = "MidPrice", Type = "decimal", Description = "Calculated mid price" },
                new SchemaField { Name = "Spread", Type = "decimal", Description = "Bid-ask spread" }
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
                new SchemaField { Name = "Timestamp", Type = "datetime", Description = "Snapshot timestamp in UTC", Format = "ISO8601" },
                new SchemaField { Name = "Symbol", Type = "string", Description = "Ticker symbol" },
                new SchemaField { Name = "Bids", Type = "array", Description = "Array of bid price levels" },
                new SchemaField { Name = "Asks", Type = "array", Description = "Array of ask price levels" },
                new SchemaField { Name = "Depth", Type = "int32", Description = "Number of levels on each side" },
                new SchemaField { Name = "Exchange", Type = "string", Description = "Exchange code", ExchangeSpecific = true }
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
                new SchemaField { Name = "Timestamp", Type = "datetime", Description = "Bar start time in UTC", Format = "ISO8601" },
                new SchemaField { Name = "Symbol", Type = "string", Description = "Ticker symbol" },
                new SchemaField { Name = "Open", Type = "decimal", Description = "Opening price" },
                new SchemaField { Name = "High", Type = "decimal", Description = "Highest price during interval" },
                new SchemaField { Name = "Low", Type = "decimal", Description = "Lowest price during interval" },
                new SchemaField { Name = "Close", Type = "decimal", Description = "Closing price" },
                new SchemaField { Name = "Volume", Type = "int64", Description = "Total volume during interval" },
                new SchemaField { Name = "VWAP", Type = "decimal", Description = "Volume-weighted average price", Nullable = true },
                new SchemaField { Name = "TradeCount", Type = "int32", Description = "Number of trades during interval", Nullable = true },
                new SchemaField { Name = "Resolution", Type = "string", Description = "Bar resolution", EnumValues = new[] { "Tick", "Second", "Minute", "Hour", "Daily" } }
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
            ["MEMX"] = "Members Exchange"
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
            ["E"] = "Automatic Execution",
            ["F"] = "Intermarket Sweep",
            ["T"] = "Form T",
            ["X"] = "Cross Trade"
        };
    }

    private static Dictionary<string, string> GetQuoteConditions()
    {
        return new Dictionary<string, string>
        {
            ["A"] = "Slow Quote Offer Side",
            ["B"] = "Slow Quote Bid Side",
            ["C"] = "Closing",
            ["O"] = "Opening",
            ["Q"] = "Regular",
            ["X"] = "Closed"
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
            sb.AppendLine("| Field | Type | Description | Nullable |");
            sb.AppendLine("|-------|------|-------------|----------|");

            foreach (var field in schema.Fields)
            {
                var nullable = field.Nullable ? "Yes" : "No";
                sb.AppendLine($"| {field.Name} | {field.Type} | {field.Description ?? ""} | {nullable} |");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

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
        }

        return sb.ToString();
    }

    private static string ExportAsCsv(DataDictionary dictionary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("EventType,FieldName,Type,Description,Nullable,Format");

        foreach (var schema in dictionary.Schemas.Values.OrderBy(s => s.Name))
        {
            foreach (var field in schema.Fields)
            {
                var description = field.Description?.Replace("\"", "\"\"") ?? "";
                sb.AppendLine($"\"{schema.Name}\",\"{field.Name}\",\"{field.Type}\",\"{description}\",\"{field.Nullable}\",\"{field.Format ?? ""}\"");
            }
        }

        return sb.ToString();
    }
}

public class DataDictionaryEventArgs : EventArgs
{
    public DataDictionary? Dictionary { get; set; }
}

public class DataDictionary
{
    public string Version { get; set; } = "2.0";
    public DateTime GeneratedAt { get; set; }
    public Dictionary<string, EventSchema> Schemas { get; set; } = new();
    public Dictionary<string, string>? ExchangeCodes { get; set; }
    public Dictionary<string, string>? TradeConditions { get; set; }
    public Dictionary<string, string>? QuoteConditions { get; set; }
}

public class EventSchema
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public DateTime IntroducedAt { get; set; }
    public SchemaField[] Fields { get; set; } = Array.Empty<SchemaField>();
    public string[]? PrimaryKey { get; set; }
    public string[][]? Indexes { get; set; }
    public Dictionary<string, object>? SampleRecord { get; set; }
}

public class SchemaField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Format { get; set; }
    public bool Nullable { get; set; }
    public string[]? EnumValues { get; set; }
    public bool ExchangeSpecific { get; set; }
    public object? Example { get; set; }
    public string? Notes { get; set; }
    public FieldValidRange? ValidRange { get; set; }
}

public class FieldValidRange
{
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
}
