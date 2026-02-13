using FluentAssertions;
using MarketDataCollector.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MarketDataCollector.Tests.Infrastructure.ProviderSdk;

/// <summary>
/// Tests for <see cref="CredentialValidator"/> — centralized credential validation utilities
/// used by all providers to validate API keys and secrets.
/// </summary>
public sealed class CredentialValidatorTests
{
    #region ValidateApiKey

    [Fact]
    public void ValidateApiKey_WithValidKey_ReturnsTrue()
    {
        var result = CredentialValidator.ValidateApiKey("valid-api-key", "TestProvider");
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateApiKey_WithNullKey_ReturnsFalse()
    {
        var result = CredentialValidator.ValidateApiKey(null, "TestProvider");
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateApiKey_WithEmptyKey_ReturnsFalse()
    {
        var result = CredentialValidator.ValidateApiKey(string.Empty, "TestProvider");
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateApiKey_WithNullKey_LogsDebugMessage()
    {
        var mockLogger = new Mock<ILogger>();
        CredentialValidator.ValidateApiKey(null, "Alpaca", mockLogger.Object);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ValidateApiKey_WithNullLogger_DoesNotThrow()
    {
        var action = () => CredentialValidator.ValidateApiKey(null, "TestProvider", null);
        action.Should().NotThrow();
    }

    #endregion

    #region ValidateKeySecretPair

    [Fact]
    public void ValidateKeySecretPair_WithBothValues_ReturnsTrue()
    {
        var result = CredentialValidator.ValidateKeySecretPair("key-id", "secret-key", "TestProvider");
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateKeySecretPair_WithNullKeyId_ReturnsFalse()
    {
        var result = CredentialValidator.ValidateKeySecretPair(null, "secret-key", "TestProvider");
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateKeySecretPair_WithNullSecretKey_ReturnsFalse()
    {
        var result = CredentialValidator.ValidateKeySecretPair("key-id", null, "TestProvider");
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateKeySecretPair_WithEmptyKeyId_ReturnsFalse()
    {
        var result = CredentialValidator.ValidateKeySecretPair(string.Empty, "secret-key", "TestProvider");
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateKeySecretPair_WithEmptySecretKey_ReturnsFalse()
    {
        var result = CredentialValidator.ValidateKeySecretPair("key-id", string.Empty, "TestProvider");
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateKeySecretPair_WithBothNull_ReturnsFalse()
    {
        var result = CredentialValidator.ValidateKeySecretPair(null, null, "TestProvider");
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateKeySecretPair_WithMissingCredentials_LogsDebugMessage()
    {
        var mockLogger = new Mock<ILogger>();
        CredentialValidator.ValidateKeySecretPair(null, null, "Alpaca", mockLogger.Object);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region ThrowIfApiKeyMissing

    [Fact]
    public void ThrowIfApiKeyMissing_WithValidKey_DoesNotThrow()
    {
        var action = () => CredentialValidator.ThrowIfApiKeyMissing("valid-key", "TestProvider", "TEST_API_KEY");
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfApiKeyMissing_WithNullKey_ThrowsInvalidOperationException()
    {
        var action = () => CredentialValidator.ThrowIfApiKeyMissing(null, "Polygon", "POLYGON__APIKEY");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Polygon*")
            .WithMessage("*POLYGON__APIKEY*");
    }

    [Fact]
    public void ThrowIfApiKeyMissing_WithEmptyKey_ThrowsInvalidOperationException()
    {
        var action = () => CredentialValidator.ThrowIfApiKeyMissing(string.Empty, "Finnhub", "FINNHUB__TOKEN");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Finnhub*")
            .WithMessage("*FINNHUB__TOKEN*");
    }

    #endregion

    #region ThrowIfCredentialsMissing

    [Fact]
    public void ThrowIfCredentialsMissing_WithBothValues_DoesNotThrow()
    {
        var action = () => CredentialValidator.ThrowIfCredentialsMissing(
            "key-id", "secret-key", "Alpaca", "ALPACA__KEYID", "ALPACA__SECRETKEY");
        action.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfCredentialsMissing_WithNullKeyId_Throws()
    {
        var action = () => CredentialValidator.ThrowIfCredentialsMissing(
            null, "secret-key", "Alpaca", "ALPACA__KEYID", "ALPACA__SECRETKEY");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Alpaca*")
            .WithMessage("*ALPACA__KEYID*")
            .WithMessage("*ALPACA__SECRETKEY*");
    }

    [Fact]
    public void ThrowIfCredentialsMissing_WithNullSecretKey_Throws()
    {
        var action = () => CredentialValidator.ThrowIfCredentialsMissing(
            "key-id", null, "Alpaca", "ALPACA__KEYID", "ALPACA__SECRETKEY");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Alpaca*");
    }

    [Fact]
    public void ThrowIfCredentialsMissing_WithBothEmpty_Throws()
    {
        var action = () => CredentialValidator.ThrowIfCredentialsMissing(
            string.Empty, string.Empty, "TestProvider", "KEY_VAR", "SECRET_VAR");
        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region GetCredential (single env var)

    [Fact]
    public void GetCredential_WithParamValue_ReturnsParamValue()
    {
        var result = CredentialValidator.GetCredential("direct-value", "UNUSED_ENV_VAR");
        result.Should().Be("direct-value");
    }

    [Fact]
    public void GetCredential_WithNullParam_FallsBackToEnvironmentVariable()
    {
        var envVarName = $"MDC_TEST_CRED_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(envVarName, "env-value");
            var result = CredentialValidator.GetCredential(null, envVarName);
            result.Should().Be("env-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void GetCredential_WithNullParamAndNoEnvVar_ReturnsNull()
    {
        var result = CredentialValidator.GetCredential(null, "NONEXISTENT_ENV_VAR_12345");
        result.Should().BeNull();
    }

    #endregion

    #region GetCredential (multiple env vars)

    [Fact]
    public void GetCredential_MultipleEnvVars_WithParamValue_ReturnsParamValue()
    {
        var result = CredentialValidator.GetCredential("direct-value", "VAR1", "VAR2", "VAR3");
        result.Should().Be("direct-value");
    }

    [Fact]
    public void GetCredential_MultipleEnvVars_ReturnsFirstFoundEnvVar()
    {
        var envVar1 = $"MDC_TEST_MULTI1_{Guid.NewGuid():N}";
        var envVar2 = $"MDC_TEST_MULTI2_{Guid.NewGuid():N}";
        try
        {
            // Only set the second one
            Environment.SetEnvironmentVariable(envVar2, "found-in-second");
            var result = CredentialValidator.GetCredential(null, envVar1, envVar2);
            result.Should().Be("found-in-second");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar1, null);
            Environment.SetEnvironmentVariable(envVar2, null);
        }
    }

    [Fact]
    public void GetCredential_MultipleEnvVars_PrefersFirstEnvVar()
    {
        var envVar1 = $"MDC_TEST_PREF1_{Guid.NewGuid():N}";
        var envVar2 = $"MDC_TEST_PREF2_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(envVar1, "first-value");
            Environment.SetEnvironmentVariable(envVar2, "second-value");
            var result = CredentialValidator.GetCredential(null, envVar1, envVar2);
            result.Should().Be("first-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar1, null);
            Environment.SetEnvironmentVariable(envVar2, null);
        }
    }

    [Fact]
    public void GetCredential_MultipleEnvVars_NoneFound_ReturnsNull()
    {
        var result = CredentialValidator.GetCredential(null, "NONEXISTENT_1", "NONEXISTENT_2");
        result.Should().BeNull();
    }

    [Fact]
    public void GetCredential_EmptyParamValue_FallsBackToEnvVar()
    {
        // Empty string is not null/empty? Actually string.IsNullOrEmpty catches empty
        // But the method checks !string.IsNullOrEmpty(paramValue)
        var envVar = $"MDC_TEST_EMPTY_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(envVar, "env-fallback");
            var result = CredentialValidator.GetCredential(string.Empty, envVar);
            result.Should().Be("env-fallback");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    #endregion
}
