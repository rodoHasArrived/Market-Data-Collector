using FluentAssertions;
using MarketDataCollector.Application.Config;
using Xunit;

namespace MarketDataCollector.Tests;

public class MassTransitConfigTests
{
    [Fact]
    public void MassTransitConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new MassTransitConfig();

        // Assert
        config.Enabled.Should().BeFalse();
        config.Transport.Should().Be("InMemory");
        config.RabbitMQ.Should().BeNull();
        config.AzureServiceBus.Should().BeNull();
        config.Retry.Should().BeNull();
        config.EnableScheduling.Should().BeFalse();
        config.EndpointPrefix.Should().BeNull();
    }

    [Fact]
    public void RabbitMqConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new RabbitMqConfig();

        // Assert
        config.Host.Should().Be("localhost");
        config.Port.Should().Be(5672);
        config.VirtualHost.Should().Be("/");
        config.Username.Should().Be("guest");
        config.Password.Should().Be("guest");
        config.UseSsl.Should().BeFalse();
        config.PublisherConfirmation.Should().BeTrue();
        config.ClusterNodes.Should().BeNull();
    }

    [Fact]
    public void AzureServiceBusConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new AzureServiceBusConfig();

        // Assert
        config.ConnectionString.Should().BeEmpty();
        config.Namespace.Should().BeNull();
        config.UseManagedIdentity.Should().BeFalse();
        config.EnablePremiumFeatures.Should().BeFalse();
    }

    [Fact]
    public void RetryConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new RetryConfig();

        // Assert
        config.MaxRetries.Should().Be(3);
        config.InitialIntervalMs.Should().Be(100);
        config.MaxIntervalMs.Should().Be(5000);
        config.IntervalMultiplier.Should().Be(2.0);
    }

    [Fact]
    public void MassTransitConfig_WithCustomValues_PreservesValues()
    {
        // Arrange & Act
        var config = new MassTransitConfig(
            Enabled: true,
            Transport: "RabbitMQ",
            RabbitMQ: new RabbitMqConfig(
                Host: "rabbitmq.example.com",
                Port: 5673,
                VirtualHost: "/prod",
                Username: "admin",
                Password: "secret",
                UseSsl: true,
                PublisherConfirmation: true,
                ClusterNodes: new[] { "node1", "node2" }
            ),
            Retry: new RetryConfig(
                MaxRetries: 5,
                InitialIntervalMs: 200,
                MaxIntervalMs: 10000,
                IntervalMultiplier: 3.0
            ),
            EnableScheduling: true,
            EndpointPrefix: "mdc-prod"
        );

        // Assert
        config.Enabled.Should().BeTrue();
        config.Transport.Should().Be("RabbitMQ");
        config.RabbitMQ.Should().NotBeNull();
        config.RabbitMQ!.Host.Should().Be("rabbitmq.example.com");
        config.RabbitMQ.Port.Should().Be(5673);
        config.RabbitMQ.VirtualHost.Should().Be("/prod");
        config.RabbitMQ.Username.Should().Be("admin");
        config.RabbitMQ.Password.Should().Be("secret");
        config.RabbitMQ.UseSsl.Should().BeTrue();
        config.RabbitMQ.ClusterNodes.Should().HaveCount(2);
        config.Retry.Should().NotBeNull();
        config.Retry!.MaxRetries.Should().Be(5);
        config.EnableScheduling.Should().BeTrue();
        config.EndpointPrefix.Should().Be("mdc-prod");
    }

    [Fact]
    public void MassTransitTransport_Enum_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<MassTransitTransport>().Should().HaveCount(3);
        MassTransitTransport.InMemory.Should().Be(MassTransitTransport.InMemory);
        MassTransitTransport.RabbitMQ.Should().Be(MassTransitTransport.RabbitMQ);
        MassTransitTransport.AzureServiceBus.Should().Be(MassTransitTransport.AzureServiceBus);
    }
}
