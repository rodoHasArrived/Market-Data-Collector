using FluentAssertions;
using Meridian.Contracts.SecurityMaster;
using Meridian.Infrastructure.SecurityMaster;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Meridian.Tests.Infrastructure.SecurityMaster;

public sealed class SecurityMasterServiceTests : IAsyncDisposable
{
    private readonly string _tempFile;
    private readonly SecurityMasterService _sut;

    public SecurityMasterServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"sm-test-{Guid.NewGuid():N}.json");
        _sut = new SecurityMasterService(_tempFile, NullLogger<SecurityMasterService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
        if (File.Exists(_tempFile + ".tmp")) File.Delete(_tempFile + ".tmp");
    }

    // ── Registration ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_ReturnsNewInstrumentId()
    {
        var reg = EquityRegistration("AAPL", "US0231351067");

        var id = await _sut.RegisterAsync(reg);

        id.Should().NotBe(InstrumentId.Empty);
    }

    [Fact]
    public async Task RegisterAsync_Idempotent_SameExternalId_ReturnsSameId()
    {
        var reg = EquityRegistration("AAPL", "US0231351067");

        var id1 = await _sut.RegisterAsync(reg);
        var id2 = await _sut.RegisterAsync(reg);

        id2.Should().Be(id1);
    }

    [Fact]
    public async Task RegisterAsync_DifferentExternalIds_ReturnDifferentIds()
    {
        var id1 = await _sut.RegisterAsync(EquityRegistration("AAPL", "US0231351067"));
        var id2 = await _sut.RegisterAsync(EquityRegistration("MSFT", "US5949181045"));

        id2.Should().NotBe(id1);
    }

    // ── GetById ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_AfterRegister_ReturnsRecord()
    {
        var reg = EquityRegistration("AAPL", "US0231351067");
        var id = await _sut.RegisterAsync(reg);

        var record = await _sut.GetByIdAsync(id);

        record.Should().NotBeNull();
        record!.DisplaySymbol.Should().Be("AAPL");
        record.Kind.Should().Be(InstrumentKind.Equity);
        record.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var record = await _sut.GetByIdAsync(InstrumentId.New());

        record.Should().BeNull();
    }

    // ── Resolve ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ByIsin_ReturnsId()
    {
        var reg = EquityRegistration("AAPL", "US0231351067");
        var id = await _sut.RegisterAsync(reg);

        var resolved = await _sut.ResolveAsync("US0231351067", ExternalIdType.Isin, "test");

        resolved.Should().Be(id);
    }

    [Fact]
    public async Task ResolveAsync_UnknownSymbol_ReturnsNull()
    {
        var resolved = await _sut.ResolveAsync("UNKNOWN-ISIN", ExternalIdType.Isin, "test");

        resolved.Should().BeNull();
    }

    // ── GetUnderlying ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUnderlyingAsync_ForEquityOption_ReturnsEquity()
    {
        var equityId = await _sut.RegisterAsync(EquityRegistration("SPY", "US78462F1030"));
        var optionReg = new InstrumentRegistration
        {
            Kind              = InstrumentKind.EquityOption,
            DisplaySymbol     = "SPY   240119C00450000",
            Currency          = "USD",
            ExerciseStyle     = ExerciseStyle.American,
            OptionSide        = OptionSide.Call,
            Strike            = 450m,
            Expiry            = new DateOnly(2024, 1, 19),
            UnderlyingId      = equityId,
            ContractMultiplier = 100m,
            SettlementType    = SettlementType.PhysicalDelivery,
            ExternalIds       = [new ExternalId { IdType = ExternalIdType.OccSymbol,
                                                  IdValue = "SPY   240119C00450000", Source = "test" }],
        };
        var optionId = await _sut.RegisterAsync(optionReg);

        var underlying = await _sut.GetUnderlyingAsync(optionId);

        underlying.Should().NotBeNull();
        underlying!.Id.Should().Be(equityId);
        underlying.DisplaySymbol.Should().Be("SPY");
    }

    [Fact]
    public async Task GetUnderlyingAsync_ForEquity_ReturnsNull()
    {
        var equityId = await _sut.RegisterAsync(EquityRegistration("AAPL", "US0231351067"));

        var underlying = await _sut.GetUnderlyingAsync(equityId);

        underlying.Should().BeNull();
    }

    // ── External IDs & Symbol History ────────────────────────────────────────

    [Fact]
    public async Task GetExternalIdsAsync_AfterRegister_ReturnsIds()
    {
        var id = await _sut.RegisterAsync(EquityRegistration("AAPL", "US0231351067"));

        var externalIds = await _sut.GetExternalIdsAsync(id);

        externalIds.Should().ContainSingle(e =>
            e.IdType == ExternalIdType.Isin && e.IdValue == "US0231351067");
    }

    // ── Corporate Actions ────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyCorporateActionAsync_SymbolChange_UpdatesDisplaySymbol()
    {
        var id = await _sut.RegisterAsync(EquityRegistration("FB", "US30303M1027"));

        await _sut.ApplyCorporateActionAsync(new CorporateAction
        {
            InstrumentId  = id,
            Kind          = CorporateActionKind.SymbolChange,
            ExDate        = new DateOnly(2022, 6, 9),
            AnnounceDate  = new DateOnly(2022, 2, 4),
            NewSymbol     = "META",
            Source        = "test",
        });

        var record = await _sut.GetByIdAsync(id);
        record!.DisplaySymbol.Should().Be("META");
    }

    [Fact]
    public async Task ApplyCorporateActionAsync_SymbolChange_AddsSymbolHistoryEntry()
    {
        var id = await _sut.RegisterAsync(EquityRegistration("FB", "US30303M1027"));

        await _sut.ApplyCorporateActionAsync(new CorporateAction
        {
            InstrumentId  = id,
            Kind          = CorporateActionKind.SymbolChange,
            ExDate        = new DateOnly(2022, 6, 9),
            AnnounceDate  = new DateOnly(2022, 2, 4),
            NewSymbol     = "META",
            Source        = "test",
        });

        var history = await _sut.GetSymbolHistoryAsync(id);
        history.Should().ContainSingle(s => s.Symbol == "FB");
    }

    [Fact]
    public async Task ApplyCorporateActionAsync_Delisting_SetsInactive()
    {
        var id = await _sut.RegisterAsync(EquityRegistration("XYZ", "US9999999999"));

        await _sut.ApplyCorporateActionAsync(new CorporateAction
        {
            InstrumentId = id,
            Kind         = CorporateActionKind.Delisting,
            ExDate       = new DateOnly(2024, 3, 1),
            AnnounceDate = new DateOnly(2024, 2, 15),
            Source       = "test",
        });

        var record = await _sut.GetByIdAsync(id);
        record!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyCorporateActionAsync_OptionAdjustment_UpdatesStrikeAndMultiplier()
    {
        var underlyingId = await _sut.RegisterAsync(EquityRegistration("SPY", "US78462F1030"));
        var optionReg = new InstrumentRegistration
        {
            Kind               = InstrumentKind.EquityOption,
            DisplaySymbol      = "SPY   240119C00450000",
            Currency           = "USD",
            ExerciseStyle      = ExerciseStyle.American,
            OptionSide         = OptionSide.Call,
            Strike             = 450m,
            Expiry             = new DateOnly(2024, 1, 19),
            UnderlyingId       = underlyingId,
            ContractMultiplier = 100m,
            SettlementType     = SettlementType.PhysicalDelivery,
            ExternalIds        = [new ExternalId { IdType = ExternalIdType.OccSymbol,
                                                   IdValue = "SPY_ADJ", Source = "test" }],
        };
        var optionId = await _sut.RegisterAsync(optionReg);

        await _sut.ApplyCorporateActionAsync(new CorporateAction
        {
            InstrumentId  = optionId,
            Kind          = CorporateActionKind.OptionAdjustment,
            ExDate        = new DateOnly(2024, 1, 5),
            AnnounceDate  = new DateOnly(2024, 1, 3),
            NewStrike     = 225m,
            NewMultiplier = 200m,
            Source        = "test",
        });

        var record = await _sut.GetByIdAsync(optionId);
        record!.Strike.Should().Be(225m);
        record.ContractMultiplier.Should().Be(200m);
    }

    // ── Search ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ByDisplaySymbolPrefix_ReturnsMatch()
    {
        await _sut.RegisterAsync(EquityRegistration("AAPL", "US0231351067"));
        await _sut.RegisterAsync(EquityRegistration("AAPLW", "US0231351068"));
        await _sut.RegisterAsync(EquityRegistration("MSFT", "US5949181045"));

        var results = await CollectAsync(_sut.SearchAsync("AAP", CancellationToken.None));

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.DisplaySymbol.Should().StartWith("AAP"));
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        await _sut.RegisterAsync(EquityRegistration("AAPL", "US0231351067"));

        var results = await CollectAsync(_sut.SearchAsync("", CancellationToken.None));

        results.Should().BeEmpty();
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Persist_AndReload_RestoresAllData()
    {
        var id = await _sut.RegisterAsync(EquityRegistration("AAPL", "US0231351067"));

        // Reload in a fresh instance from the same file
        await using var reloaded = new SecurityMasterService(
            _tempFile, NullLogger<SecurityMasterService>.Instance);
        await reloaded.LoadAsync();

        var record = await reloaded.GetByIdAsync(id);
        record.Should().NotBeNull();
        record!.DisplaySymbol.Should().Be("AAPL");

        var resolved = await reloaded.ResolveAsync("US0231351067", ExternalIdType.Isin, "test");
        resolved.Should().Be(id);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static InstrumentRegistration EquityRegistration(string symbol, string isin) =>
        new()
        {
            Kind          = InstrumentKind.Equity,
            DisplaySymbol = symbol,
            Currency      = "USD",
            ExternalIds   = [new ExternalId { IdType = ExternalIdType.Isin, IdValue = isin, Source = "test" }],
        };
}
