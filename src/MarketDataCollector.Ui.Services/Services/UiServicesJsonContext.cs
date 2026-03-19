using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketDataCollector.Contracts.Configuration;

namespace MarketDataCollector.Ui.Services;

/// <summary>
/// Source-generated JSON serialization context for the shared UI services layer.
/// Registers all configuration DTO types used by <see cref="ConfigService"/>,
/// replacing reflection-based serialization per ADR-014.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(AppConfigDto))]
[JsonSerializable(typeof(AppSettingsDto))]
[JsonSerializable(typeof(AlpacaOptionsDto))]
[JsonSerializable(typeof(PolygonOptionsDto))]
[JsonSerializable(typeof(IBOptionsDto))]
[JsonSerializable(typeof(StockSharpOptionsDto))]
[JsonSerializable(typeof(RithmicOptionsDto))]
[JsonSerializable(typeof(IQFeedOptionsDto))]
[JsonSerializable(typeof(CQGOptionsDto))]
[JsonSerializable(typeof(StockSharpIBOptionsDto))]
[JsonSerializable(typeof(StorageConfigDto))]
[JsonSerializable(typeof(SymbolConfigDto))]
[JsonSerializable(typeof(SymbolConfigDto[]))]
[JsonSerializable(typeof(List<SymbolConfigDto>))]
[JsonSerializable(typeof(ExtendedSymbolConfigDto))]
[JsonSerializable(typeof(BackfillConfigDto))]
[JsonSerializable(typeof(BackfillProvidersConfigDto))]
[JsonSerializable(typeof(BackfillProviderOptionsDto))]
[JsonSerializable(typeof(BackfillProviderMetadataDto))]
[JsonSerializable(typeof(BackfillProviderStatusDto))]
[JsonSerializable(typeof(BackfillDryRunPlanDto))]
[JsonSerializable(typeof(BackfillSymbolPlanDto))]
[JsonSerializable(typeof(ProviderConfigAuditEntryDto))]
[JsonSerializable(typeof(DataSourcesConfigDto))]
[JsonSerializable(typeof(DataSourceConfigDto))]
[JsonSerializable(typeof(DataSourceConfigDto[]))]
[JsonSerializable(typeof(List<DataSourceConfigDto>))]
[JsonSerializable(typeof(SymbolGroupsConfigDto))]
[JsonSerializable(typeof(SymbolGroupDto))]
[JsonSerializable(typeof(SmartGroupCriteriaDto))]
[JsonSerializable(typeof(DerivativesConfigDto))]
[JsonSerializable(typeof(IndexOptionsConfigDto))]
public partial class UiServicesJsonContext : JsonSerializerContext
{
    /// <summary>
    /// Pre-configured options for config file serialization.
    /// - Indented output for human readability
    /// - Case-insensitive property matching on read
    /// - Null values omitted
    /// - Source-generated serializers (no reflection)
    /// </summary>
    public static readonly JsonSerializerOptions ConfigFileOptions = new()
    {
        TypeInfoResolver = Default,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}
