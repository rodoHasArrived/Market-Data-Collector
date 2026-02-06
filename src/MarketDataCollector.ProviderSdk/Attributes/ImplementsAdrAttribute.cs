namespace MarketDataCollector.ProviderSdk.Attributes;

/// <summary>
/// Marks a class as implementing a specific Architectural Decision Record (ADR).
/// Provider plugins use this to declare compliance with documented architectural contracts.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class ImplementsAdrAttribute : Attribute
{
    /// <summary>
    /// The ADR identifier (e.g., "ADR-001").
    /// </summary>
    public string AdrId { get; }

    /// <summary>
    /// Optional description of how this type implements the ADR.
    /// </summary>
    public string? Description { get; set; }

    public ImplementsAdrAttribute(string adrId, string? description = null)
    {
        AdrId = adrId ?? throw new ArgumentNullException(nameof(adrId));
        Description = description;
    }
}
