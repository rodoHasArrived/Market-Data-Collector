using FluentAssertions;
using MarketDataCollector.Application.Services;
using Xunit;

namespace MarketDataCollector.Tests.Services;

/// <summary>
/// Tests for <see cref="SensitiveValueMasker"/> (QW-78).
/// </summary>
public sealed class SensitiveValueMaskerTests
{
    #region IsSensitiveKey Tests

    [Theory]
    [InlineData("password", true)]
    [InlineData("PASSWORD", true)]
    [InlineData("userPassword", true)]
    [InlineData("secret", true)]
    [InlineData("api_secret", true)]
    [InlineData("apikey", true)]
    [InlineData("api_key", true)]
    [InlineData("api-key", true)]
    [InlineData("secretKey", true)]
    [InlineData("ALPACA_SECRET_KEY", true)]
    [InlineData("connectionstring", true)]
    [InlineData("token", true)]
    [InlineData("bearer_token", true)]
    [InlineData("auth", true)]
    [InlineData("authorization", true)]
    [InlineData("credential", true)]
    [InlineData("privateKey", true)]
    [InlineData("certificate", true)]
    [InlineData("signing_key", true)]
    public void IsSensitiveKey_WithSensitiveKeys_ReturnsTrue(string key, bool expected)
    {
        SensitiveValueMasker.IsSensitiveKey(key).Should().Be(expected);
    }

    [Theory]
    [InlineData("username", false)]
    [InlineData("email", false)]
    [InlineData("host", false)]
    [InlineData("port", false)]
    [InlineData("dataRoot", false)]
    [InlineData("environment", false)]
    [InlineData("logLevel", false)]
    [InlineData("symbolCount", false)]
    public void IsSensitiveKey_WithNonSensitiveKeys_ReturnsFalse(string key, bool expected)
    {
        SensitiveValueMasker.IsSensitiveKey(key).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSensitiveKey_WithNullOrEmpty_ReturnsFalse(string? key)
    {
        SensitiveValueMasker.IsSensitiveKey(key).Should().BeFalse();
    }

    #endregion

    #region MaskValue Tests

    [Fact]
    public void MaskValue_WithValue_ReturnsMasked()
    {
        var result = SensitiveValueMasker.MaskValue("my-secret-value");
        result.Should().Be("[REDACTED]");
    }

    [Fact]
    public void MaskValue_WithShowLength_IncludesLength()
    {
        var result = SensitiveValueMasker.MaskValue("my-secret-value", showLength: true);
        result.Should().Be("[REDACTED] (15 chars)");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MaskValue_WithNullOrEmpty_ReturnsRedacted(string? value)
    {
        SensitiveValueMasker.MaskValue(value).Should().Be("[REDACTED]");
    }

    #endregion

    #region MaskValueWithHint Tests

    [Fact]
    public void MaskValueWithHint_WithLongValue_ShowsPrefix()
    {
        var result = SensitiveValueMasker.MaskValueWithHint("sk-1234567890abcdef");
        result.Should().Be("sk-1...[REDACTED]");
    }

    [Fact]
    public void MaskValueWithHint_WithCustomVisibleChars_ShowsPrefix()
    {
        var result = SensitiveValueMasker.MaskValueWithHint("pk_live_abcdefghijk", visibleChars: 8);
        result.Should().Be("pk_live_...[REDACTED]");
    }

    [Fact]
    public void MaskValueWithHint_WithShortValue_ReturnsMasked()
    {
        var result = SensitiveValueMasker.MaskValueWithHint("abc");
        result.Should().Be("[REDACTED]");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MaskValueWithHint_WithNullOrEmpty_ReturnsRedacted(string? value)
    {
        SensitiveValueMasker.MaskValueWithHint(value).Should().Be("[REDACTED]");
    }

    #endregion

    #region MaskDictionary Tests

    [Fact]
    public void MaskDictionary_MasksSensitiveKeys()
    {
        var dict = new Dictionary<string, string>
        {
            ["host"] = "localhost",
            ["port"] = "5432",
            ["password"] = "super-secret",
            ["apiKey"] = "pk_live_xyz123"
        };

        var result = SensitiveValueMasker.MaskDictionary(dict);

        result["host"].Should().Be("localhost");
        result["port"].Should().Be("5432");
        result["password"].Should().Be("[REDACTED]");
        result["apiKey"].Should().Be("[REDACTED]");
    }

    [Fact]
    public void MaskDictionary_WithNull_ReturnsEmptyDictionary()
    {
        var result = SensitiveValueMasker.MaskDictionary(null);
        result.Should().BeEmpty();
    }

    #endregion

    #region SanitizeContent Tests

    [Fact]
    public void SanitizeContent_MasksPasswordPatterns()
    {
        var content = "Connecting with password=mysecret123 to server";
        var result = SensitiveValueMasker.SanitizeContent(content);
        result.Should().NotContain("mysecret123");
        result.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void SanitizeContent_MasksBearerTokens()
    {
        var content = "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.signature";
        var result = SensitiveValueMasker.SanitizeContent(content);
        result.Should().NotContain("eyJhbGci");
        result.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void SanitizeContent_MasksBasicAuth()
    {
        var content = "Authorization: Basic dXNlcm5hbWU6cGFzc3dvcmQ=";
        var result = SensitiveValueMasker.SanitizeContent(content);
        result.Should().NotContain("dXNlcm5hbWU");
        result.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void SanitizeContent_MasksConnectionStringPasswords()
    {
        var content = "Server=myserver;Database=mydb;User ID=myuser;Password=p@ssw0rd!";
        var result = SensitiveValueMasker.SanitizeContent(content);
        result.Should().NotContain("p@ssw0rd!");
        result.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void SanitizeContent_MasksJsonStringValues()
    {
        var content = """{"apiKey": "sk-1234567890abcdef", "host": "localhost"}""";
        var result = SensitiveValueMasker.SanitizeContent(content);
        result.Should().NotContain("sk-1234567890abcdef");
        result.Should().Contain("[REDACTED]");
        result.Should().Contain("localhost"); // Non-sensitive values preserved
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void SanitizeContent_WithNullOrEmpty_ReturnsEmpty(string? content, string expected)
    {
        SensitiveValueMasker.SanitizeContent(content).Should().Be(expected);
    }

    [Fact]
    public void SanitizeContent_PreservesNonSensitiveData()
    {
        var content = "INFO: Processing symbol=AAPL with count=100";
        var result = SensitiveValueMasker.SanitizeContent(content);
        result.Should().Be(content);
    }

    #endregion

    #region SanitizeJson Tests

    [Fact]
    public void SanitizeJson_MasksSensitiveProperties()
    {
        var json = """
        {
            "host": "localhost",
            "port": 5432,
            "password": "super-secret",
            "nested": {
                "apiKey": "pk_live_xyz123",
                "timeout": 30
            }
        }
        """;

        var result = SensitiveValueMasker.SanitizeJson(json);

        result.Should().Contain("\"host\"");
        result.Should().Contain("localhost");
        result.Should().NotContain("super-secret");
        result.Should().NotContain("pk_live_xyz123");
        result.Should().Contain("[REDACTED]");
        result.Should().Contain("\"timeout\"");
    }

    [Fact]
    public void SanitizeJson_HandlesInvalidJson()
    {
        var invalidJson = "{ invalid json }";
        // Should not throw, falls back to regex sanitization
        var result = SensitiveValueMasker.SanitizeJson(invalidJson);
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void SanitizeJson_WithNullOrEmpty_ReturnsEmpty(string? json, string expected)
    {
        SensitiveValueMasker.SanitizeJson(json).Should().Be(expected);
    }

    #endregion

    #region FormatConfigSummary Tests

    [Fact]
    public void FormatConfigSummary_MasksSensitiveValues()
    {
        var config = new Dictionary<string, object?>
        {
            ["host"] = "localhost",
            ["apiKey"] = "sk-secret-key",
            ["password"] = "mypassword",
            ["port"] = 8080,
            ["enabled"] = true,
            ["notSet"] = null
        };

        var result = SensitiveValueMasker.FormatConfigSummary(config);

        result.Should().Contain("host: localhost");
        result.Should().Contain("sk-s...[REDACTED]"); // apiKey with hint
        result.Should().Contain("mypa...[REDACTED]"); // password with hint
        result.Should().Contain("port: 8080");
        result.Should().Contain("enabled: true");
        result.Should().Contain("notSet: (not set)");
    }

    #endregion

    #region FormatSafeException Tests

    [Fact]
    public void FormatSafeException_MasksSensitiveDataInMessage()
    {
        var exception = new Exception("Connection failed with password=secret123");
        var result = SensitiveValueMasker.FormatSafeException(exception);

        result.Should().NotContain("secret123");
        result.Should().Contain("[REDACTED]");
        result.Should().Contain("Connection failed");
    }

    [Fact]
    public void FormatSafeException_IncludesInnerException()
    {
        var inner = new Exception("Inner with token=abc123");
        var outer = new Exception("Outer error", inner);
        var result = SensitiveValueMasker.FormatSafeException(outer);

        result.Should().Contain("Inner Exception:");
        result.Should().NotContain("abc123");
    }

    #endregion

    #region GetSensitiveKeyPatterns Tests

    [Fact]
    public void GetSensitiveKeyPatterns_ReturnsNonEmptyList()
    {
        var patterns = SensitiveValueMasker.GetSensitiveKeyPatterns();
        patterns.Should().NotBeEmpty();
        patterns.Should().Contain("password");
        patterns.Should().Contain("secret");
        patterns.Should().Contain("apikey");
    }

    #endregion
}
