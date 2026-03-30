namespace Meridian.Contracts.SecurityMaster;

/// <summary>
/// Stable, permanent identifier for any financial instrument.
/// Assigned once at registration — never changes on ticker change, corporate action, relist, or exchange migration.
/// All market data ingestion, ledger posting, and analytics work with InstrumentId — never raw ticker strings.
/// </summary>
public readonly record struct InstrumentId(Guid Value)
{
    /// <summary>Create a new unique InstrumentId.</summary>
    public static InstrumentId New() => new(Guid.NewGuid());

    /// <summary>Empty sentinel — use to indicate "no instrument" (not a valid registration).</summary>
    public static readonly InstrumentId Empty = new(Guid.Empty);

    /// <summary>True if this is the Empty sentinel.</summary>
    public bool IsEmpty => Value == Guid.Empty;

    /// <summary>Parse from a Guid string. Throws on invalid input.</summary>
    public static InstrumentId Parse(string s) => new(Guid.Parse(s));

    /// <summary>Try parse from a Guid string. Returns false if the string is not a valid Guid.</summary>
    public static bool TryParse(string s, out InstrumentId result)
    {
        if (Guid.TryParse(s, out var g)) { result = new(g); return true; }
        result = Empty;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}
