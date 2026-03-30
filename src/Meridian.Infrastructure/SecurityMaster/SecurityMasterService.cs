using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Meridian.Contracts.SecurityMaster;
using Microsoft.Extensions.Logging;

namespace Meridian.Infrastructure.SecurityMaster;

/// <summary>
/// Thread-safe, in-memory Security Master with JSON persistence.
/// <para>
/// On startup the service loads all records from the configured data file path.
/// Every mutating operation (Register, ApplyCorporateAction) persists immediately.
/// Reads never touch disk after startup — O(1) via concurrent dictionaries.
/// </para>
/// </summary>
public sealed class SecurityMasterService : ISecurityMasterService, IAsyncDisposable
{
    // ── Primary store ─────────────────────────────────────────────────────────
    private readonly ConcurrentDictionary<InstrumentId, InstrumentRecord> _records = new();

    // ── Secondary indexes ─────────────────────────────────────────────────────
    // (idType, value) → InstrumentId for the most recent active entry per type+value
    private readonly ConcurrentDictionary<ExternalIdKey, InstrumentId> _externalIdIndex = new();

    // InstrumentId → all ExternalId entries (active + historical)
    private readonly ConcurrentDictionary<InstrumentId, List<ExternalId>> _externalIds = new();

    // InstrumentId → symbol history
    private readonly ConcurrentDictionary<InstrumentId, List<SymbolEntry>> _symbolHistory = new();

    // ── Write serialization ───────────────────────────────────────────────────
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // ── Persistence ───────────────────────────────────────────────────────────
    private readonly string _dataFilePath;
    private readonly ILogger<SecurityMasterService> _logger;

    public SecurityMasterService(string dataFilePath, ILogger<SecurityMasterService> logger)
    {
        _dataFilePath = dataFilePath;
        _logger = logger;
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Load persisted data from disk. Call once at startup before accepting requests.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_dataFilePath))
        {
            _logger.LogInformation("SecurityMaster data file not found at {Path}; starting empty", _dataFilePath);
            return;
        }

        var snapshot = await ReadSnapshotAsync(ct);
        if (snapshot is null) return;

        foreach (var record in snapshot.Records)
            _records[record.Id] = record;

        foreach (var eid in snapshot.ExternalIds)
        {
            _externalIds.GetOrAdd(eid.InstrumentId, _ => []).Add(eid);
            if (eid.ValidTo is null)
                _externalIdIndex[new ExternalIdKey(eid.IdType, eid.IdValue)] = eid.InstrumentId;
        }

        foreach (var sym in snapshot.SymbolEntries)
            _symbolHistory.GetOrAdd(sym.InstrumentId, _ => []).Add(sym);

        _logger.LogInformation(
            "SecurityMaster loaded: {RecordCount} instruments, {ExternalIdCount} external IDs, {SymbolCount} symbol entries",
            snapshot.Records.Count, snapshot.ExternalIds.Count, snapshot.SymbolEntries.Count);
    }

    // ── ISecurityMasterService ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask<InstrumentId> RegisterAsync(
        InstrumentRegistration registration,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        await _writeLock.WaitAsync(ct);
        try
        {
            // Idempotency: check every supplied external ID for an existing match
            foreach (var eid in registration.ExternalIds)
            {
                var key = new ExternalIdKey(eid.IdType, eid.IdValue);
                if (_externalIdIndex.TryGetValue(key, out var existingId))
                {
                    _logger.LogDebug(
                        "RegisterAsync: idempotent — {IdType}:{IdValue} already maps to {InstrumentId}",
                        eid.IdType, eid.IdValue, existingId);
                    return existingId;
                }
            }

            // New instrument
            var id = InstrumentId.New();
            var now = DateTimeOffset.UtcNow;

            var record = new InstrumentRecord
            {
                Id                  = id,
                Kind                = registration.Kind,
                DisplaySymbol       = registration.DisplaySymbol,
                Currency            = registration.Currency,
                ContractMultiplier  = registration.ContractMultiplier,
                SettlementType      = registration.SettlementType,
                ExerciseStyle       = registration.ExerciseStyle,
                OptionSide          = registration.OptionSide,
                Strike              = registration.Strike,
                Expiry              = registration.Expiry,
                UnderlyingId        = registration.UnderlyingId,
                PrimaryExchangeMic  = registration.PrimaryExchangeMic,
                IssuerName          = registration.IssuerName,
                TickSize            = registration.TickSize,
                TickValue           = registration.TickValue,
                CouponRate          = registration.CouponRate,
                MaturityDate        = registration.MaturityDate,
                FaceValue           = registration.FaceValue,
                BaseCurrency        = registration.BaseCurrency,
                QuoteCurrency       = registration.QuoteCurrency,
                FxSettlementDate    = registration.FxSettlementDate,
                IsActive            = true,
                RegisteredAt        = now,
                UpdatedAt           = now,
            };

            _records[id] = record;

            // Index external IDs
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var externalIds = new List<ExternalId>(registration.ExternalIds.Count);
            foreach (var eid in registration.ExternalIds)
            {
                var entry = new ExternalId
                {
                    InstrumentId = id,
                    IdType       = eid.IdType,
                    IdValue      = eid.IdValue,
                    ValidFrom    = today,
                    ValidTo      = null,
                    Source       = eid.Source,
                };
                externalIds.Add(entry);
                _externalIds.GetOrAdd(id, _ => []).Add(entry);
                _externalIdIndex[new ExternalIdKey(eid.IdType, eid.IdValue)] = id;
            }

            await PersistAsync(ct);

            _logger.LogInformation(
                "Registered instrument {InstrumentId} ({Kind} {Symbol})",
                id, registration.Kind, registration.DisplaySymbol);

            return id;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask<InstrumentRecord?> GetByIdAsync(InstrumentId id, CancellationToken ct = default)
    {
        _records.TryGetValue(id, out var record);
        return ValueTask.FromResult(record);
    }

    /// <inheritdoc/>
    public ValueTask<InstrumentId?> ResolveAsync(
        string externalSymbol,
        ExternalIdType idType,
        string source,
        DateOnly? asOf = null,
        CancellationToken ct = default)
    {
        var key = new ExternalIdKey(idType, externalSymbol);
        if (!_externalIdIndex.TryGetValue(key, out var instrumentId))
        {
            // Not in the active-entry index — check historical entries with time-validity
            asOf ??= DateOnly.FromDateTime(DateTime.UtcNow);
            foreach (var (id, entries) in _externalIds)
            {
                foreach (var entry in entries)
                {
                    if (entry.IdType == idType && entry.IdValue == externalSymbol
                        && (string.IsNullOrEmpty(source) || entry.Source == source)
                        && entry.IsActive(asOf.Value))
                    {
                        return ValueTask.FromResult<InstrumentId?>(entry.InstrumentId);
                    }
                }
            }
            return ValueTask.FromResult<InstrumentId?>(null);
        }

        return ValueTask.FromResult<InstrumentId?>(instrumentId);
    }

    /// <inheritdoc/>
    public ValueTask<InstrumentRecord?> GetUnderlyingAsync(
        InstrumentId derivativeId,
        CancellationToken ct = default)
    {
        if (!_records.TryGetValue(derivativeId, out var derivative))
            return ValueTask.FromResult<InstrumentRecord?>(null);

        if (derivative.UnderlyingId is not { } underlyingId)
            return ValueTask.FromResult<InstrumentRecord?>(null);

        _records.TryGetValue(underlyingId, out var underlying);
        return ValueTask.FromResult(underlying);
    }

    /// <inheritdoc/>
    public async ValueTask ApplyCorporateActionAsync(CorporateAction action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _writeLock.WaitAsync(ct);
        try
        {
            if (!_records.TryGetValue(action.InstrumentId, out var current))
            {
                _logger.LogWarning(
                    "ApplyCorporateAction: unknown instrument {InstrumentId}, action {Kind} ignored",
                    action.InstrumentId, action.Kind);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            InstrumentRecord updated = current;

            switch (action.Kind)
            {
                case CorporateActionKind.SymbolChange when action.NewSymbol is not null:
                    // Retire old symbol entry
                    AppendSymbolEntry(current, action.ExDate, action.Source);
                    updated = current with { DisplaySymbol = action.NewSymbol, UpdatedAt = now };
                    break;

                case CorporateActionKind.Merger when action.RelatedInstrumentId.HasValue:
                    updated = current with { IsActive = false, UpdatedAt = now };
                    break;

                case CorporateActionKind.Delisting:
                    updated = current with { IsActive = false, UpdatedAt = now };
                    break;

                case CorporateActionKind.OptionAdjustment:
                    updated = current with
                    {
                        Strike             = action.NewStrike ?? current.Strike,
                        ContractMultiplier = action.NewMultiplier ?? current.ContractMultiplier,
                        UpdatedAt          = now,
                    };
                    break;

                case CorporateActionKind.StockSplit:
                case CorporateActionKind.StockDividend:
                case CorporateActionKind.CashDividend:
                case CorporateActionKind.SpinOff:
                    // Reference data change is noted; position/cost-basis adjustments
                    // are the responsibility of the ledger layer, not the Security Master.
                    updated = current with { UpdatedAt = now };
                    break;
            }

            _records[action.InstrumentId] = updated;
            await PersistAsync(ct);

            _logger.LogInformation(
                "Applied corporate action {Kind} to instrument {InstrumentId} (ex-date {ExDate})",
                action.Kind, action.InstrumentId, action.ExDate);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
#pragma warning disable CS1998 // Sync iterator — no async I/O needed; async is required for yield + IAsyncEnumerable
    public async IAsyncEnumerable<InstrumentRecord> SearchAsync(
        string query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            yield break;

        var q = query.Trim();
        foreach (var record in _records.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (record.DisplaySymbol.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                || (record.IssuerName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                yield return record;
            }
        }
    }

#pragma warning restore CS1998

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ExternalId>> GetExternalIdsAsync(
        InstrumentId id,
        CancellationToken ct = default)
    {
        if (_externalIds.TryGetValue(id, out var list))
            return ValueTask.FromResult<IReadOnlyList<ExternalId>>(list.ToArray());

        return ValueTask.FromResult<IReadOnlyList<ExternalId>>([]);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SymbolEntry>> GetSymbolHistoryAsync(
        InstrumentId id,
        CancellationToken ct = default)
    {
        if (_symbolHistory.TryGetValue(id, out var list))
            return ValueTask.FromResult<IReadOnlyList<SymbolEntry>>(list.ToArray());

        return ValueTask.FromResult<IReadOnlyList<SymbolEntry>>([]);
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void AppendSymbolEntry(InstrumentRecord record, DateOnly exDate, string source)
    {
        var entry = new SymbolEntry
        {
            InstrumentId = record.Id,
            Symbol       = record.DisplaySymbol,
            ExchangeMic  = record.PrimaryExchangeMic ?? "",
            ValidFrom    = DateOnly.MinValue,
            ValidTo      = exDate,
            Source       = source,
        };
        _symbolHistory.GetOrAdd(record.Id, _ => []).Add(entry);
    }

    // ── Persistence helpers ───────────────────────────────────────────────────

    private async Task PersistAsync(CancellationToken ct)
    {
        var snapshot = new SecurityMasterSnapshot
        {
            Records       = [.. _records.Values],
            ExternalIds   = [.. _externalIds.Values.SelectMany(x => x)],
            SymbolEntries = [.. _symbolHistory.Values.SelectMany(x => x)],
        };

        var dir = Path.GetDirectoryName(_dataFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmpPath = _dataFilePath + ".tmp";
        await using var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 65536, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, snapshot,
            SecurityMasterJsonContext.Default.SecurityMasterSnapshot, ct);
        await stream.FlushAsync(ct);
        stream.Close();

        File.Move(tmpPath, _dataFilePath, overwrite: true);
    }

    private async Task<SecurityMasterSnapshot?> ReadSnapshotAsync(CancellationToken ct)
    {
        try
        {
            await using var stream = new FileStream(_dataFilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 65536, useAsync: true);
            return await JsonSerializer.DeserializeAsync(stream,
                SecurityMasterJsonContext.Default.SecurityMasterSnapshot, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read SecurityMaster snapshot from {Path}", _dataFilePath);
            return null;
        }
    }
}

// ── Internal types ────────────────────────────────────────────────────────────

/// <summary>Key used to index external identifiers — (type, value) tuple.</summary>
internal readonly record struct ExternalIdKey(ExternalIdType IdType, string IdValue);

/// <summary>Persisted snapshot of all Security Master state.</summary>
internal sealed class SecurityMasterSnapshot
{
    public List<InstrumentRecord> Records { get; set; } = [];
    public List<ExternalId> ExternalIds { get; set; } = [];
    public List<SymbolEntry> SymbolEntries { get; set; } = [];
}
