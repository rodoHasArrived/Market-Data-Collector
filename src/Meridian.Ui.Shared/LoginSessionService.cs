using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Meridian.Ui.Shared;

/// <summary>
/// In-memory session store for username/password authentication.
/// Credentials are read from the MDC_USERNAME and MDC_PASSWORD environment variables.
/// When neither variable is set the service reports as not configured and all requests
/// pass through without authentication (backward-compatible).
/// </summary>
public sealed class LoginSessionService
{
    private const string UsernameEnvVar = "MDC_USERNAME";
    private const string PasswordEnvVar = "MDC_PASSWORD";

    /// <summary>Session lifetime; exposed so the auth endpoint can set a matching cookie expiry.</summary>
    internal static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();

    /// <summary>
    /// Returns true when both MDC_USERNAME and MDC_PASSWORD are configured via environment variables.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UsernameEnvVar)) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PasswordEnvVar));

    /// <summary>
    /// Validates the supplied credentials and creates a new session token on success.
    /// Returns null when the credentials are invalid or authentication is not configured.
    /// </summary>
    public string? CreateSession(string username, string password)
    {
        var expectedUsername = Environment.GetEnvironmentVariable(UsernameEnvVar);
        var expectedPassword = Environment.GetEnvironmentVariable(PasswordEnvVar);

        if (string.IsNullOrWhiteSpace(expectedUsername) || string.IsNullOrWhiteSpace(expectedPassword))
            return null;

        if (!CryptographicEquals(username, expectedUsername) ||
            !CryptographicEquals(password, expectedPassword))
            return null;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = new SessionEntry(username, DateTimeOffset.UtcNow + SessionDuration);
        PruneExpiredSessions();
        return token;
    }

    /// <summary>
    /// Returns true when the token corresponds to a valid, non-expired session.
    /// </summary>
    public bool ValidateSession(string token)
    {
        if (!_sessions.TryGetValue(token, out var entry))
            return false;

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Removes the session associated with the given token (logout).
    /// </summary>
    public void RemoveSession(string token) => _sessions.TryRemove(token, out _);

    private void PruneExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (token, entry) in _sessions)
        {
            if (entry.ExpiresAt <= now)
                _sessions.TryRemove(token, out _);
        }
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks on credential validation.
    /// </summary>
    private static bool CryptographicEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));

    private sealed record SessionEntry(string Username, DateTimeOffset ExpiresAt);
}
