using System.Text.Json.Serialization;
using Meridian.Contracts.SecurityMaster;

namespace Meridian.Infrastructure.SecurityMaster;

/// <summary>
/// Source-generated JSON context for Security Master persistence types.
/// Eliminates reflection overhead per ADR-014.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(SecurityMasterSnapshot))]
[JsonSerializable(typeof(InstrumentRecord))]
[JsonSerializable(typeof(List<InstrumentRecord>))]
[JsonSerializable(typeof(ExternalId))]
[JsonSerializable(typeof(List<ExternalId>))]
[JsonSerializable(typeof(SymbolEntry))]
[JsonSerializable(typeof(List<SymbolEntry>))]
[JsonSerializable(typeof(InstrumentId))]
[JsonSerializable(typeof(InstrumentKind))]
[JsonSerializable(typeof(ExternalIdType))]
[JsonSerializable(typeof(ExerciseStyle))]
[JsonSerializable(typeof(SettlementType))]
[JsonSerializable(typeof(OptionSide))]
internal partial class SecurityMasterJsonContext : JsonSerializerContext
{
}
