using FluentAssertions;
using MarketDataCollector.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.Providers;

public sealed class CredentialValidatorTests
{
    // ── ValidateApiKey ──────────────────────────────────────────────

    [Fact]
    public void ValidateApiKey_WithValidKey_ReturnsTrue()
    {
        CredentialValidator.ValidateApiKey("pk_live_abc123", "TestProvider")
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateApiKey_WithMissingKey_ReturnsFalse(string? apiKey)
    {
        CredentialValidator.ValidateApiKey(apiKey, "TestProvider")
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateApiKey_WithMissingKey_LogsDebugMessage(string? apiKey)
    {
        var mockLogger = new Mock<ILogger>();
        CredentialValidator.ValidateApiKey(apiKey, "Alpaca", mockLogger.Object);

        // Verify that a log message was written (Debug level)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ValidateApiKey_WithNullLogger_DoesNotThrow()
    {
        var act = () => CredentialValidator.ValidateApiKey(null, "TestProvider", null);
        act.Should().NotThrow();
    }

    // ── ValidateKeySecretPair ───────────────────────────────────────

    [Fact]
    public void ValidateKeySecretPair_WithBothValues_ReturnsTrue()
    {
        CredentialValidator.ValidateKeySecretPair("keyId", "secret", "TestProvider")
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("", "secret")]
    [InlineData("keyId", null)]
    [InlineData("keyId", "")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void ValidateKeySecretPair_WithMissingCredential_ReturnsFalse(string? keyId, string? secret)
    {
        CredentialValidator.ValidateKeySecretPair(keyId, secret, "TestProvider")
            .Should().BeFalse();
    }

    // ── ThrowIfApiKeyMissing ────────────────────────────────────────

    [Fact]
    public void ThrowIfApiKeyMissing_WithValidKey_DoesNotThrow()
    {
        var act = () => CredentialValidator.ThrowIfApiKeyMissing("valid-key", "Polygon", "POLYGON__APIKEY");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ThrowIfApiKeyMissing_WithMissingKey_ThrowsInvalidOperation(string? apiKey)
    {
        var act = () => CredentialValidator.ThrowIfApiKeyMissing(apiKey, "Polygon", "POLYGON__APIKEY");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Polygon*")
            .WithMessage("*POLYGON__APIKEY*");
    }

    // ── ThrowIfCredentialsMissing ───────────────────────────────────

    [Fact]
    public void ThrowIfCredentialsMissing_WithBothValues_DoesNotThrow()
    {
        var act = () => CredentialValidator.ThrowIfCredentialsMissing(
            "key", "secret", "Alpaca", "ALPACA__KEYID", "ALPACA__SECRETKEY");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("key", null)]
    [InlineData(null, null)]
    [InlineData("", "secret")]
    [InlineData("key", "")]
    public void ThrowIfCredentialsMissing_WithMissing_ThrowsInvalidOperation(string? keyId, string? secret)
    {
        var act = () => CredentialValidator.ThrowIfCredentialsMissing(
            keyId, secret, "Alpaca", "ALPACA__KEYID", "ALPACA__SECRETKEY");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Alpaca*")
            .WithMessage("*ALPACA__KEYID*")
            .WithMessage("*ALPACA__SECRETKEY*");
    }

    // ── GetCredential (single env var) ──────────────────────────────

    [Fact]
    public void GetCredential_WithParamValue_ReturnsParam()
    {
        CredentialValidator.GetCredential("direct-value", "UNUSED_ENV_VAR")
            .Should().Be("direct-value");
    }

    [Fact]
    public void GetCredential_WithNullParam_FallsBackToEnvVar()
    {
        // Use a unique env var name to avoid collisions
        const string envVar = "MDC_TEST_CRED_SINGLE_12345";
        try
        {
            Environment.SetEnvironmentVariable(envVar, "env-value");
            CredentialValidator.GetCredential(null, envVar)
                .Should().Be("env-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [Fact]
    public void GetCredential_WithNullParamAndNoEnvVar_ReturnsNull()
    {
        CredentialValidator.GetCredential(null, "MDC_NONEXISTENT_ENV_VAR_XYZ_99999")
            .Should().BeNull();
    }

    // ── GetCredential (multiple env vars) ───────────────────────────

    [Fact]
    public void GetCredentialMultiple_WithParamValue_ReturnsParam()
    {
        CredentialValidator.GetCredential("direct-value", "ENV1", "ENV2")
            .Should().Be("direct-value");
    }

    [Fact]
    public void GetCredentialMultiple_TriesEnvVarsInOrder()
    {
        const string envVar1 = "MDC_TEST_CRED_MULTI_1";
        const string envVar2 = "MDC_TEST_CRED_MULTI_2";
        try
        {
            Environment.SetEnvironmentVariable(envVar1, null);
            Environment.SetEnvironmentVariable(envVar2, "second-value");

            CredentialValidator.GetCredential(null, envVar1, envVar2)
                .Should().Be("second-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar1, null);
            Environment.SetEnvironmentVariable(envVar2, null);
        }
    }

    [Fact]
    public void GetCredentialMultiple_ReturnsFirstFoundEnvVar()
    {
        const string envVar1 = "MDC_TEST_CRED_FIRST_1";
        const string envVar2 = "MDC_TEST_CRED_FIRST_2";
        try
        {
            Environment.SetEnvironmentVariable(envVar1, "first-value");
            Environment.SetEnvironmentVariable(envVar2, "second-value");

            CredentialValidator.GetCredential(null, envVar1, envVar2)
                .Should().Be("first-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar1, null);
            Environment.SetEnvironmentVariable(envVar2, null);
        }
    }

    [Fact]
    public void GetCredentialMultiple_WithNoMatchingEnvVars_ReturnsNull()
    {
        CredentialValidator.GetCredential(
                null,
                "MDC_NONEXISTENT_A_99999",
                "MDC_NONEXISTENT_B_99999")
            .Should().BeNull();
    }

    [Fact]
    public void GetCredential_WithEmptyString_ReturnsEmptyViaCoalesce()
    {
        // The single-param overload uses null-coalescing, so "" (not null) passes through
        CredentialValidator.GetCredential("", "MDC_NONEXISTENT_ENV_123")
            .Should().Be("");
    }
}
