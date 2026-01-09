using System.Text.Json.Serialization;

namespace MarketDataCollector.Contracts.Schema;

/// <summary>
/// Schema definition for a data event type.
/// </summary>
public sealed class EventSchema
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("introducedAt")]
    public DateTime IntroducedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deprecatedAt")]
    public DateTime? DeprecatedAt { get; set; }

    [JsonPropertyName("fields")]
    public SchemaField[] Fields { get; set; } = Array.Empty<SchemaField>();

    [JsonPropertyName("primaryKey")]
    public string[]? PrimaryKey { get; set; }

    [JsonPropertyName("indexes")]
    public string[][]? Indexes { get; set; }

    [JsonPropertyName("migrationFromVersion")]
    public string? MigrationFromVersion { get; set; }

    [JsonPropertyName("sampleRecord")]
    public Dictionary<string, object>? SampleRecord { get; set; }
}

/// <summary>
/// Schema field definition.
/// </summary>
public sealed class SchemaField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("nullable")]
    public bool Nullable { get; set; }

    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("validRange")]
    public FieldValidRange? ValidRange { get; set; }

    [JsonPropertyName("enumValues")]
    public string[]? EnumValues { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("example")]
    public object? Example { get; set; }

    [JsonPropertyName("exchangeSpecific")]
    public bool ExchangeSpecific { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Valid range for a field.
/// </summary>
public sealed class FieldValidRange
{
    [JsonPropertyName("min")]
    public object? Min { get; set; }

    [JsonPropertyName("max")]
    public object? Max { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }
}

/// <summary>
/// Data dictionary containing all schemas.
/// </summary>
public sealed class DataDictionary
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("schemas")]
    public Dictionary<string, EventSchema> Schemas { get; set; } = new();

    [JsonPropertyName("exchangeCodes")]
    public Dictionary<string, string>? ExchangeCodes { get; set; }

    [JsonPropertyName("tradeConditions")]
    public Dictionary<string, string>? TradeConditions { get; set; }

    [JsonPropertyName("quoteConditions")]
    public Dictionary<string, string>? QuoteConditions { get; set; }
}
