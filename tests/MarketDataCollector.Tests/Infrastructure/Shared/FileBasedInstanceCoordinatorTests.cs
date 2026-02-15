using FluentAssertions;
using MarketDataCollector.Infrastructure.Shared;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Shared;

/// <summary>
/// Unit tests for FileBasedInstanceCoordinator — the file-based multi-instance
/// symbol coordination layer (H2 from the project roadmap).
/// </summary>
public sealed class FileBasedInstanceCoordinatorTests : IAsyncLifetime
{
    private string _testDir = null!;

    public Task InitializeAsync()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"mdc-coordinator-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true); }
        catch { /* cleanup best effort */ }
        return Task.CompletedTask;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesClaimsDirectory()
    {
        var dir = Path.Combine(_testDir, "sub", "claims");

        using var coordinator = CreateCoordinator(dir);

        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullDirectory_ThrowsArgumentException()
    {
        var act = () => new FileBasedInstanceCoordinator(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyDirectory_ThrowsArgumentException()
    {
        var act = () => new FileBasedInstanceCoordinator("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InstanceId_DefaultsToMachineNameAndPid()
    {
        using var coordinator = CreateCoordinator();

        coordinator.InstanceId.Should().Contain(Environment.MachineName);
    }

    [Fact]
    public void InstanceId_UsesCustomValueWhenProvided()
    {
        using var coordinator = new FileBasedInstanceCoordinator(
            Path.Combine(_testDir, "claims"),
            instanceId: "my-instance-42");

        coordinator.InstanceId.Should().Be("my-instance-42");
    }

    #endregion

    #region TryClaimSymbol Tests

    [Fact]
    public async Task TryClaimSymbol_NewSymbol_ReturnsTrue()
    {
        await using var coordinator = CreateCoordinator();

        var claimed = await coordinator.TryClaimSymbolAsync("SPY");

        claimed.Should().BeTrue();
        coordinator.GetOwnedSymbols().Should().Contain("SPY");
    }

    [Fact]
    public async Task TryClaimSymbol_AlreadyOwnedBySameInstance_ReturnsTrue()
    {
        await using var coordinator = CreateCoordinator();

        await coordinator.TryClaimSymbolAsync("SPY");
        var claimed = await coordinator.TryClaimSymbolAsync("SPY");

        claimed.Should().BeTrue();
    }

    [Fact]
    public async Task TryClaimSymbol_OwnedByOtherInstance_ReturnsFalse()
    {
        var claimsDir = Path.Combine(_testDir, "shared-claims");
        await using var instance1 = new FileBasedInstanceCoordinator(claimsDir, instanceId: "instance-1");
        await using var instance2 = new FileBasedInstanceCoordinator(claimsDir, instanceId: "instance-2");

        var claimed1 = await instance1.TryClaimSymbolAsync("SPY");
        var claimed2 = await instance2.TryClaimSymbolAsync("SPY");

        claimed1.Should().BeTrue();
        claimed2.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimSymbol_MultipleSymbols_TracksAll()
    {
        await using var coordinator = CreateCoordinator();

        await coordinator.TryClaimSymbolAsync("SPY");
        await coordinator.TryClaimSymbolAsync("AAPL");
        await coordinator.TryClaimSymbolAsync("MSFT");

        coordinator.GetOwnedSymbols().Should().HaveCount(3);
    }

    [Fact]
    public async Task TryClaimSymbol_NormalizesToUppercase()
    {
        await using var coordinator = CreateCoordinator();

        await coordinator.TryClaimSymbolAsync("spy");

        coordinator.GetOwnedSymbols().Should().Contain("SPY");
    }

    [Fact]
    public async Task TryClaimSymbol_EmptySymbol_ThrowsArgumentException()
    {
        await using var coordinator = CreateCoordinator();

        var act = () => coordinator.TryClaimSymbolAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region ReleaseSymbol Tests

    [Fact]
    public async Task ReleaseSymbol_RemovesFromOwnedSymbols()
    {
        await using var coordinator = CreateCoordinator();
        await coordinator.TryClaimSymbolAsync("SPY");

        await coordinator.ReleaseSymbolAsync("SPY");

        coordinator.GetOwnedSymbols().Should().NotContain("SPY");
    }

    [Fact]
    public async Task ReleaseSymbol_AllowsOtherInstanceToClaim()
    {
        var claimsDir = Path.Combine(_testDir, "shared-claims-release");
        await using var instance1 = new FileBasedInstanceCoordinator(claimsDir, instanceId: "instance-1");
        await using var instance2 = new FileBasedInstanceCoordinator(claimsDir, instanceId: "instance-2");

        await instance1.TryClaimSymbolAsync("SPY");
        await instance1.ReleaseSymbolAsync("SPY");
        var claimed = await instance2.TryClaimSymbolAsync("SPY");

        claimed.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseSymbol_UnknownSymbol_DoesNotThrow()
    {
        await using var coordinator = CreateCoordinator();

        var act = () => coordinator.ReleaseSymbolAsync("UNKNOWN");

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region RefreshHeartbeat Tests

    [Fact]
    public async Task RefreshHeartbeat_KeepsClaimsAlive()
    {
        var claimsDir = Path.Combine(_testDir, "heartbeat-claims");
        await using var instance1 = new FileBasedInstanceCoordinator(
            claimsDir, instanceId: "instance-1", heartbeatTimeout: TimeSpan.FromMilliseconds(200));

        await instance1.TryClaimSymbolAsync("SPY");

        // Wait, then refresh
        await Task.Delay(100);
        await instance1.RefreshHeartbeatAsync();

        // Wait again — claim should still be alive due to heartbeat
        await Task.Delay(100);

        await using var instance2 = new FileBasedInstanceCoordinator(
            claimsDir, instanceId: "instance-2", heartbeatTimeout: TimeSpan.FromMilliseconds(200));

        var claimed = await instance2.TryClaimSymbolAsync("SPY");
        claimed.Should().BeFalse("instance-1 refreshed its heartbeat");
    }

    #endregion

    #region StaleClaim Tests

    [Fact]
    public async Task TryClaimSymbol_StaleClaimFromOtherInstance_Reclaims()
    {
        var claimsDir = Path.Combine(_testDir, "stale-claims");
        // Very short timeout for test speed
        await using var instance1 = new FileBasedInstanceCoordinator(
            claimsDir, instanceId: "instance-1", heartbeatTimeout: TimeSpan.FromMilliseconds(100));

        await instance1.TryClaimSymbolAsync("SPY");

        // Wait for heartbeat to expire
        await Task.Delay(200);

        await using var instance2 = new FileBasedInstanceCoordinator(
            claimsDir, instanceId: "instance-2", heartbeatTimeout: TimeSpan.FromMilliseconds(100));

        var claimed = await instance2.TryClaimSymbolAsync("SPY");
        claimed.Should().BeTrue("instance-1's claim expired");
    }

    [Fact]
    public async Task ReclaimStale_RemovesExpiredClaims()
    {
        var claimsDir = Path.Combine(_testDir, "reclaim-claims");
        await using var instance1 = new FileBasedInstanceCoordinator(
            claimsDir, instanceId: "instance-1", heartbeatTimeout: TimeSpan.FromMilliseconds(100));

        await instance1.TryClaimSymbolAsync("SPY");
        await instance1.TryClaimSymbolAsync("AAPL");

        // Wait for heartbeat to expire (don't refresh)
        await Task.Delay(200);

        await using var instance2 = new FileBasedInstanceCoordinator(
            claimsDir, instanceId: "instance-2", heartbeatTimeout: TimeSpan.FromMilliseconds(100));

        var reclaimed = await instance2.ReclaimStaleAsync();

        reclaimed.Should().Be(2);
    }

    #endregion

    #region GetAllClaims Tests

    [Fact]
    public async Task GetAllClaims_ReturnsActiveClaims()
    {
        var claimsDir = Path.Combine(_testDir, "all-claims");
        await using var instance1 = new FileBasedInstanceCoordinator(claimsDir, instanceId: "instance-1");
        await using var instance2 = new FileBasedInstanceCoordinator(claimsDir, instanceId: "instance-2");

        await instance1.TryClaimSymbolAsync("SPY");
        await instance2.TryClaimSymbolAsync("AAPL");

        var claims = await instance1.GetAllClaimsAsync();

        claims.Should().HaveCount(2);
        claims["SPY"].Should().Be("instance-1");
        claims["AAPL"].Should().Be("instance-2");
    }

    [Fact]
    public async Task GetAllClaims_ExcludesStaleClaims()
    {
        var claimsDir = Path.Combine(_testDir, "exclude-stale");
        await using var instance1 = new FileBasedInstanceCoordinator(
            claimsDir, instanceId: "instance-1", heartbeatTimeout: TimeSpan.FromMilliseconds(100));

        await instance1.TryClaimSymbolAsync("SPY");

        // Wait for claim to expire
        await Task.Delay(200);

        var claims = await instance1.GetAllClaimsAsync();

        claims.Should().BeEmpty("stale claims should be excluded");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task Dispose_ReleasesAllOwnedSymbols()
    {
        var claimsDir = Path.Combine(_testDir, "dispose-claims");

        var instance1 = new FileBasedInstanceCoordinator(claimsDir, instanceId: "instance-1");
        await instance1.TryClaimSymbolAsync("SPY");
        await instance1.TryClaimSymbolAsync("AAPL");
        await instance1.DisposeAsync();

        await using var instance2 = new FileBasedInstanceCoordinator(claimsDir, instanceId: "instance-2");

        var claimedSpy = await instance2.TryClaimSymbolAsync("SPY");
        var claimedAapl = await instance2.TryClaimSymbolAsync("AAPL");

        claimedSpy.Should().BeTrue("instance-1 released claims on dispose");
        claimedAapl.Should().BeTrue("instance-1 released claims on dispose");
    }

    [Fact]
    public async Task TryClaimSymbol_AfterDispose_ThrowsObjectDisposedException()
    {
        var coordinator = CreateCoordinator();
        await coordinator.DisposeAsync();

        var act = () => coordinator.TryClaimSymbolAsync("SPY");

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Helpers

    private FileBasedInstanceCoordinator CreateCoordinator(string? dir = null)
    {
        return new FileBasedInstanceCoordinator(
            dir ?? Path.Combine(_testDir, "claims"));
    }

    #endregion
}
