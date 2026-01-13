using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MarketDataCollector.Application.Services;

/// <summary>
/// Utility class for masking sensitive values in logs, configuration output, and diagnostic data.
/// Implements QW-78: Sensitive Value Masking.
/// </summary>
/// <remarks>
/// <para>This class provides centralized sensitive data masking for:</para>
/// <list type="bullet">
/// <item><description>API keys and secrets</description></item>
/// <item><description>Passwords and credentials</description></item>
/// <item><description>Connection strings</description></item>
/// <item><description>Authentication tokens (Bearer, Basic)</description></item>
/// </list>
/// </remarks>
public static class SensitiveValueMasker
{
    /// <summary>
    /// Default redaction text used when masking sensitive values.
    /// </summary>
    public const string RedactedText = "[REDACTED]";

    /// <summary>
    /// Keys that are considered sensitive and should be masked.
    /// Matching is case-insensitive and checks if the key contains any of these substrings.
    /// </summary>
    private static readonly string[] SensitiveKeyPatterns =
    {
        "password",
        "secret",
        "key",
        "token",
        "apikey",
        "api_key",
        "api-key",
        "connectionstring",
        "connection_string",
        "credential",
        "auth",
        "bearer",
        "private",
        "certificate",
        "signing"
    };

    /// <summary>
    /// Regex patterns for detecting and masking sensitive values in text content.
    /// </summary>
    private static readonly (Regex Pattern, string Replacement)[] ContentPatterns =
    {
        // Key=Value patterns (passwords, secrets, keys, tokens)
        (new Regex(@"(password|secret|key|token|apikey|api_key|credential)[\s]*[:=][\s]*([^\s\]\},""']+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), "$1=" + RedactedText),

        // JSON string values for sensitive keys
        (new Regex(@"""(password|secret|key|token|apikey|api_key|credential)""\s*:\s*""[^""]*""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), "\"$1\": \"" + RedactedText + "\""),

        // Bearer tokens
        (new Regex(@"Bearer\s+[A-Za-z0-9\-_\.]+",
            RegexOptions.Compiled), "Bearer " + RedactedText),

        // Basic auth
        (new Regex(@"Basic\s+[A-Za-z0-9+/=]+",
            RegexOptions.Compiled), "Basic " + RedactedText),

        // Connection strings (various database formats)
        (new Regex(@"(Password|Pwd|User ID|UID)=[^;]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), "$1=" + RedactedText),

        // AWS access keys (AKIA pattern)
        (new Regex(@"AKIA[A-Z0-9]{16}",
            RegexOptions.Compiled), RedactedText),

        // Generic API key patterns (32+ hex characters)
        (new Regex(@"\b[a-fA-F0-9]{32,}\b",
            RegexOptions.Compiled), RedactedText),

        // JWT tokens (three base64 sections separated by dots)
        (new Regex(@"eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+",
            RegexOptions.Compiled), RedactedText)
    };

    /// <summary>
    /// Determines if a key name represents a sensitive value that should be masked.
    /// </summary>
    /// <param name="key">The key name to check.</param>
    /// <returns>True if the key is considered sensitive; otherwise, false.</returns>
    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return SensitiveKeyPatterns.Any(pattern =>
            key.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    /// Masks a sensitive value, showing only a hint of its presence.
    /// </summary>
    /// <param name="value">The value to mask.</param>
    /// <param name="showLength">If true, shows the length of the original value.</param>
    /// <returns>The masked value.</returns>
    public static string MaskValue(string? value, bool showLength = false)
    {
        if (string.IsNullOrEmpty(value))
            return RedactedText;

        if (showLength)
            return $"{RedactedText} ({value.Length} chars)";

        return RedactedText;
    }

    /// <summary>
    /// Masks a sensitive value, showing the first few characters as a hint.
    /// </summary>
    /// <param name="value">The value to mask.</param>
    /// <param name="visibleChars">Number of characters to show at the start.</param>
    /// <returns>The masked value with visible prefix.</returns>
    public static string MaskValueWithHint(string? value, int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(value))
            return RedactedText;

        if (value.Length <= visibleChars)
            return RedactedText;

        return value[..visibleChars] + "..." + RedactedText;
    }

    /// <summary>
    /// Masks sensitive values in a dictionary based on key names.
    /// </summary>
    /// <param name="dictionary">The dictionary to sanitize.</param>
    /// <returns>A new dictionary with sensitive values masked.</returns>
    public static Dictionary<string, string> MaskDictionary(IDictionary<string, string>? dictionary)
    {
        if (dictionary == null)
            return new Dictionary<string, string>();

        return dictionary.ToDictionary(
            kvp => kvp.Key,
            kvp => IsSensitiveKey(kvp.Key) ? RedactedText : kvp.Value
        );
    }

    /// <summary>
    /// Masks sensitive values in environment variables.
    /// </summary>
    /// <param name="filter">Optional filter function to select which environment variables to include.</param>
    /// <returns>Dictionary of environment variables with sensitive values masked.</returns>
    public static Dictionary<string, string> MaskEnvironmentVariables(Func<string, bool>? filter = null)
    {
        var result = new Dictionary<string, string>();

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            if (string.IsNullOrEmpty(key))
                continue;

            if (filter != null && !filter(key))
                continue;

            var value = entry.Value?.ToString() ?? string.Empty;

            if (IsSensitiveKey(key))
            {
                result[key] = RedactedText;
            }
            else if (key.Equals("PATH", StringComparison.OrdinalIgnoreCase))
            {
                result[key] = "[PATH variable - omitted for brevity]";
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Sanitizes text content by replacing patterns that may contain sensitive data.
    /// </summary>
    /// <param name="content">The text content to sanitize.</param>
    /// <returns>The sanitized content with sensitive values masked.</returns>
    public static string SanitizeContent(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var result = content;

        foreach (var (pattern, replacement) in ContentPatterns)
        {
            result = pattern.Replace(result, replacement);
        }

        return result;
    }

    /// <summary>
    /// Sanitizes a JSON string by masking sensitive property values.
    /// </summary>
    /// <param name="json">The JSON string to sanitize.</param>
    /// <returns>Sanitized JSON string.</returns>
    public static string SanitizeJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(json);
            var sanitized = SanitizeJsonElement(document.RootElement);
            return JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            // If JSON parsing fails, fall back to regex-based sanitization
            return SanitizeContent(json);
        }
    }

    /// <summary>
    /// Recursively sanitizes a JsonElement.
    /// </summary>
    private static object? SanitizeJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    if (IsSensitiveKey(property.Name))
                    {
                        obj[property.Name] = RedactedText;
                    }
                    else
                    {
                        obj[property.Name] = SanitizeJsonElement(property.Value);
                    }
                }
                return obj;

            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(SanitizeJsonElement)
                    .ToList();

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            default:
                return null;
        }
    }

    /// <summary>
    /// Formats a configuration summary with sensitive values masked.
    /// Useful for logging configuration state without exposing secrets.
    /// </summary>
    /// <param name="configValues">Key-value pairs of configuration.</param>
    /// <returns>Formatted configuration summary.</returns>
    public static string FormatConfigSummary(IEnumerable<KeyValuePair<string, object?>> configValues)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Configuration Summary:");

        foreach (var kvp in configValues)
        {
            var displayValue = kvp.Value switch
            {
                null => "(not set)",
                string s when IsSensitiveKey(kvp.Key) => MaskValueWithHint(s),
                string s when string.IsNullOrEmpty(s) => "(empty)",
                string s => s,
                bool b => b.ToString().ToLowerInvariant(),
                _ when IsSensitiveKey(kvp.Key) => RedactedText,
                _ => kvp.Value.ToString() ?? "(null)"
            };

            sb.AppendLine($"  {kvp.Key}: {displayValue}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a safe string representation of an exception, masking any sensitive data
    /// that might appear in the message or stack trace.
    /// </summary>
    /// <param name="exception">The exception to format.</param>
    /// <returns>Safe string representation of the exception.</returns>
    public static string FormatSafeException(Exception exception)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Exception Type: {exception.GetType().Name}");
        sb.AppendLine($"Message: {SanitizeContent(exception.Message)}");

        if (exception.StackTrace != null)
        {
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(SanitizeContent(exception.StackTrace));
        }

        if (exception.InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine("Inner Exception:");
            sb.Append(FormatSafeException(exception.InnerException));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a list of the sensitive key patterns used for detection.
    /// Useful for documentation or configuration.
    /// </summary>
    /// <returns>Read-only list of sensitive key patterns.</returns>
    public static IReadOnlyList<string> GetSensitiveKeyPatterns()
    {
        return SensitiveKeyPatterns;
    }
}
