using FluentAssertions;
using MarketDataCollector.Infrastructure.Providers.StockSharp.ConnectorHub;
using Xunit;

namespace MarketDataCollector.Tests.Providers.StockSharp.ConnectorHub;

/// <summary>
/// Unit tests for ConnectorHubOptions and factory methods.
/// </summary>
public sealed class ConnectorHubOptionsTests
{
    [Fact]
    public void ConnectorHubOptions_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = new ConnectorHubOptions();

        // Assert
        options.Enabled.Should().BeFalse();
        options.EnabledAdapters.Should().BeEmpty();
        options.DefaultAdapterId.Should().BeNull();
        options.UseFallbackRouting.Should().BeFalse();
        options.Adapters.Should().BeEmpty();
        options.ProviderRouting.Should().BeEmpty();
        options.ExchangeRouting.Should().BeEmpty();
        options.AssetClassRouting.Should().BeEmpty();
    }

    [Fact]
    public void HeartbeatOptions_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = new HeartbeatOptions();

        // Assert
        options.Interval.Should().Be(TimeSpan.FromSeconds(30));
        options.Timeout.Should().Be(TimeSpan.FromMinutes(2));
        options.AutoReconnect.Should().BeTrue();
    }

    [Fact]
    public void ReconnectionOptions_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = new ReconnectionOptions();

        // Assert
        options.MaxAttempts.Should().Be(10);
        options.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxDelay.Should().Be(TimeSpan.FromMinutes(15));
        options.BackoffMultiplier.Should().Be(2.0);
        options.RecoverSubscriptions.Should().BeTrue();
    }

    [Fact]
    public void BufferingOptions_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = new BufferingOptions();

        // Assert
        options.Capacity.Should().Be(10_000);
        options.FullMode.Should().Be(BufferFullMode.DropOldest);
    }

    [Fact]
    public void AdapterOptions_GetSetting_ShouldReturnCorrectTypedValues()
    {
        // Arrange
        var options = new AdapterOptions
        {
            Settings = new Dictionary<string, string>
            {
                ["IntValue"] = "42",
                ["BoolValue"] = "true",
                ["DoubleValue"] = "3.14",
                ["StringValue"] = "hello",
                ["TimeSpanValue"] = "00:05:00"
            }
        };

        // Act & Assert
        options.GetSetting("IntValue", 0).Should().Be(42);
        options.GetSetting("BoolValue", false).Should().BeTrue();
        options.GetSetting("DoubleValue", 0.0).Should().BeApproximately(3.14, 0.001);
        options.GetSetting("StringValue", "").Should().Be("hello");
        options.GetSetting("TimeSpanValue", TimeSpan.Zero).Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void AdapterOptions_GetSetting_ShouldReturnDefaultForMissingKey()
    {
        // Arrange
        var options = new AdapterOptions();

        // Act & Assert
        options.GetSetting("Missing", 99).Should().Be(99);
        options.GetSetting("Missing", "default").Should().Be("default");
        options.GetSetting("Missing", true).Should().BeTrue();
    }

    [Fact]
    public void AdapterOptions_GetSetting_ShouldReturnDefaultForInvalidFormat()
    {
        // Arrange
        var options = new AdapterOptions
        {
            Settings = new Dictionary<string, string>
            {
                ["InvalidInt"] = "not-a-number"
            }
        };

        // Act & Assert
        options.GetSetting("InvalidInt", 42).Should().Be(42);
    }

    [Fact]
    public void ConnectorHubOptionsFactory_ForInteractiveBrokers_ShouldCreateValidOptions()
    {
        // Act
        var options = ConnectorHubOptionsFactory.ForInteractiveBrokers(
            host: "192.168.1.100",
            port: 4001,
            clientId: 5);

        // Assert
        options.Enabled.Should().BeTrue();
        options.EnabledAdapters.Should().ContainSingle("ib");
        options.DefaultAdapterId.Should().Be("ib");
        options.Adapters.Should().HaveCount(1);

        var adapter = options.Adapters[0];
        adapter.Id.Should().Be("ib");
        adapter.Type.Should().Be("ib");
        adapter.Enabled.Should().BeTrue();
        adapter.GetSetting("Host", "").Should().Be("192.168.1.100");
        adapter.GetSetting("Port", 0).Should().Be(4001);
        adapter.GetSetting("ClientId", 0).Should().Be(5);
    }

    [Fact]
    public void ConnectorHubOptionsFactory_ForAlpaca_ShouldCreateValidOptions()
    {
        // Act
        var options = ConnectorHubOptionsFactory.ForAlpaca(
            keyId: "my-key",
            secretKey: "my-secret",
            usePaper: false,
            feed: "sip");

        // Assert
        options.Enabled.Should().BeTrue();
        options.EnabledAdapters.Should().ContainSingle("alpaca");
        options.DefaultAdapterId.Should().Be("alpaca");
        options.Adapters.Should().HaveCount(1);

        var adapter = options.Adapters[0];
        adapter.GetSetting("KeyId", "").Should().Be("my-key");
        adapter.GetSetting("SecretKey", "").Should().Be("my-secret");
        adapter.GetSetting("UsePaper", true).Should().BeFalse();
        adapter.GetSetting("Feed", "").Should().Be("sip");
    }

    [Fact]
    public void ConnectorHubOptionsFactory_MultiAdapter_ShouldCreateValidOptions()
    {
        // Arrange
        var ibAdapter = new AdapterOptions
        {
            Id = "ib",
            Type = "ib",
            Enabled = true,
            Priority = 10
        };
        var alpacaAdapter = new AdapterOptions
        {
            Id = "alpaca",
            Type = "alpaca",
            Enabled = true,
            Priority = 20
        };
        var disabledAdapter = new AdapterOptions
        {
            Id = "polygon",
            Type = "polygon",
            Enabled = false
        };

        // Act
        var options = ConnectorHubOptionsFactory.MultiAdapter(ibAdapter, alpacaAdapter, disabledAdapter);

        // Assert
        options.Enabled.Should().BeTrue();
        options.EnabledAdapters.Should().HaveCount(2);
        options.EnabledAdapters.Should().Contain("ib", "alpaca");
        options.EnabledAdapters.Should().NotContain("polygon");
        options.DefaultAdapterId.Should().Be("ib"); // First enabled adapter
        options.Adapters.Should().HaveCount(3);
    }

    [Fact]
    public void ConnectorHubOptions_WithCustomRouting_ShouldStoreRoutes()
    {
        // Act
        var options = new ConnectorHubOptions
        {
            Enabled = true,
            ProviderRouting = new Dictionary<string, string>
            {
                ["yahoo"] = "alpaca",
                ["stooq"] = "polygon"
            },
            ExchangeRouting = new Dictionary<string, string>
            {
                ["CME"] = "rithmic",
                ["BINANCE"] = "binance"
            },
            AssetClassRouting = new Dictionary<string, string>
            {
                ["crypto"] = "binance",
                ["futures"] = "rithmic"
            }
        };

        // Assert
        options.ProviderRouting.Should().HaveCount(2);
        options.ProviderRouting["yahoo"].Should().Be("alpaca");

        options.ExchangeRouting.Should().HaveCount(2);
        options.ExchangeRouting["CME"].Should().Be("rithmic");

        options.AssetClassRouting.Should().HaveCount(2);
        options.AssetClassRouting["crypto"].Should().Be("binance");
    }

    [Fact]
    public void ConnectorHubOptions_WithCustomHeartbeat_ShouldApplySettings()
    {
        // Act
        var options = new ConnectorHubOptions
        {
            Heartbeat = new HeartbeatOptions
            {
                Interval = TimeSpan.FromSeconds(15),
                Timeout = TimeSpan.FromMinutes(1),
                AutoReconnect = false
            }
        };

        // Assert
        options.Heartbeat.Interval.Should().Be(TimeSpan.FromSeconds(15));
        options.Heartbeat.Timeout.Should().Be(TimeSpan.FromMinutes(1));
        options.Heartbeat.AutoReconnect.Should().BeFalse();
    }

    [Fact]
    public void ConnectorHubOptions_WithCustomReconnection_ShouldApplySettings()
    {
        // Act
        var options = new ConnectorHubOptions
        {
            Reconnection = new ReconnectionOptions
            {
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromMinutes(5),
                BackoffMultiplier = 1.5,
                RecoverSubscriptions = false
            }
        };

        // Assert
        options.Reconnection.MaxAttempts.Should().Be(5);
        options.Reconnection.InitialDelay.Should().Be(TimeSpan.FromSeconds(2));
        options.Reconnection.MaxDelay.Should().Be(TimeSpan.FromMinutes(5));
        options.Reconnection.BackoffMultiplier.Should().Be(1.5);
        options.Reconnection.RecoverSubscriptions.Should().BeFalse();
    }

    [Fact]
    public void ConnectorHubOptions_WithCustomBuffering_ShouldApplySettings()
    {
        // Act
        var options = new ConnectorHubOptions
        {
            Buffering = new BufferingOptions
            {
                Capacity = 50_000,
                FullMode = BufferFullMode.Wait
            }
        };

        // Assert
        options.Buffering.Capacity.Should().Be(50_000);
        options.Buffering.FullMode.Should().Be(BufferFullMode.Wait);
    }
}
