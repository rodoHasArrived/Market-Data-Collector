using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
using Windows.Storage;
using MarketDataCollector.Uwp.Models;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for secure credential management using Windows Credential Manager
/// and CredentialPicker UI. Enhanced with OAuth support, expiration tracking,
/// and credential testing capabilities.
/// </summary>
public class CredentialService
{
    private const string ResourcePrefix = "MarketDataCollector";
    private const string MetadataFileName = "credential_metadata.json";
    private const string LogPrefix = "[CredentialService]";

    // Credential resource names
    public const string AlpacaCredentialResource = $"{ResourcePrefix}.Alpaca";
    public const string NasdaqApiKeyResource = $"{ResourcePrefix}.NasdaqDataLink";
    public const string OpenFigiApiKeyResource = $"{ResourcePrefix}.OpenFigi";
    public const string OAuthTokenResource = $"{ResourcePrefix}.OAuth";

    // Alpaca API endpoints for testing
    private const string AlpacaPaperBaseUrl = "https://paper-api.alpaca.markets";
    private const string AlpacaLiveBaseUrl = "https://api.alpaca.markets";

    private readonly PasswordVault _vault;
    private readonly HttpClient _httpClient;
    private Dictionary<string, CredentialMetadata> _metadataCache;
    private readonly string _metadataPath;

    /// <summary>
    /// Event raised when credential metadata is updated.
    /// </summary>
    public event EventHandler<CredentialMetadataEventArgs>? MetadataUpdated;

    /// <summary>
    /// Event raised when a credential is about to expire.
    /// </summary>
    public event EventHandler<CredentialExpirationEventArgs>? CredentialExpiring;

    /// <summary>
    /// Event raised when a credential operation fails.
    /// </summary>
    public event EventHandler<CredentialErrorEventArgs>? CredentialError;

    public CredentialService()
    {
        _vault = new PasswordVault();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _metadataCache = new Dictionary<string, CredentialMetadata>();
        _metadataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketDataCollector",
            MetadataFileName);

        LoadMetadataAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Prompts the user for credentials using the Windows CredentialPicker UI.
    /// </summary>
    /// <param name="targetName">The target name for the credential (displayed to user)</param>
    /// <param name="message">Message explaining what the credentials are for</param>
    /// <param name="caption">Caption for the dialog</param>
    /// <param name="saveOption">Whether to show/check the save checkbox</param>
    /// <returns>CredentialPickerResults or null if cancelled</returns>
    public async Task<CredentialPickerResults?> PromptForCredentialsAsync(
        string targetName,
        string message,
        string caption,
        CredentialSaveOption saveOption = CredentialSaveOption.Selected)
    {
        try
        {
            var options = new CredentialPickerOptions
            {
                TargetName = targetName,
                Message = message,
                Caption = caption,
                CredentialSaveOption = saveOption,
                AuthenticationProtocol = AuthenticationProtocol.Basic,
                AlwaysDisplayDialog = true
            };

            var result = await CredentialPicker.PickAsync(options);

            if (result.ErrorCode == 0 && !string.IsNullOrEmpty(result.CredentialUserName))
            {
                return result;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            // User cancelled the credential picker dialog
            LogDebug("Credential picker cancelled by user for {TargetName}", targetName);
            return null;
        }
        catch (Exception ex)
        {
            // System error during credential picker
            LogError(ex, CredentialOperation.PromptCredentials, targetName,
                "Credential picker failed for {TargetName}");
            return null;
        }
    }

    /// <summary>
    /// Prompts for API key credentials (single value, no username).
    /// </summary>
    public async Task<string?> PromptForApiKeyAsync(
        string targetName,
        string message,
        string caption)
    {
        try
        {
            var options = new CredentialPickerOptions
            {
                TargetName = targetName,
                Message = message,
                Caption = caption,
                CredentialSaveOption = CredentialSaveOption.Selected,
                AuthenticationProtocol = AuthenticationProtocol.Basic,
                AlwaysDisplayDialog = true
            };

            var result = await CredentialPicker.PickAsync(options);

            if (result.ErrorCode == 0)
            {
                // For API keys, we use the password field (or username if password is empty)
                var apiKey = !string.IsNullOrEmpty(result.CredentialPassword)
                    ? result.CredentialPassword
                    : result.CredentialUserName;

                if (!string.IsNullOrEmpty(apiKey) && result.CredentialSaved)
                {
                    SaveApiKey(targetName, apiKey);
                }

                return apiKey;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            // User cancelled the API key prompt dialog
            LogDebug("API key prompt cancelled by user for {TargetName}", targetName);
            return null;
        }
        catch (Exception ex)
        {
            // Expected exceptions: COM exceptions from CredentialPicker, ArgumentException for invalid options
            // Unexpected: Any other exception indicates a system-level issue
            LogError(ex, CredentialOperation.PromptApiKey, targetName,
                "API key prompt failed for {TargetName}");
            return null;
        }
    }

    /// <summary>
    /// Saves username/password credentials to the Windows Credential Manager.
    /// </summary>
    public void SaveCredential(string resource, string username, string password)
    {
        try
        {
            // Remove existing credential if present
            RemoveCredential(resource);

            var credential = new PasswordCredential(resource, username, password);
            _vault.Add(credential);
        }
        catch (Exception ex)
        {
            // Log the failure but don't throw - saving credentials is not critical to app operation
            // However, for production scenarios, consider whether credential save failures should be more visible
            LogError(ex, CredentialOperation.Save, resource,
                "Failed to save credential for {Resource}. User may need to re-enter credentials on next use.");
        }
    }

    /// <summary>
    /// Saves an API key (single value) to the Windows Credential Manager.
    /// </summary>
    public void SaveApiKey(string resource, string apiKey)
    {
        SaveCredential(resource, "apikey", apiKey);
    }

    /// <summary>
    /// Retrieves credentials from the Windows Credential Manager.
    /// </summary>
    /// <returns>Tuple of (username, password) or null if not found</returns>
    public (string Username, string Password)? GetCredential(string resource)
    {
        try
        {
            var credentials = _vault.FindAllByResource(resource);
            if (credentials.Count > 0)
            {
                var credential = credentials[0];
                credential.RetrievePassword();
                return (credential.UserName, credential.Password);
            }
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070490)) // Element not found
        {
            // Credential not found - this is expected for first-time access
            LogDebug("Credential not found for {Resource} (first-time access)", resource);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Access denied - credential exists but user doesn't have permission
            LogWarning(ex, CredentialOperation.Retrieve, resource,
                "Access denied when retrieving credential for {Resource}");
        }
        catch (Exception ex)
        {
            // Unexpected error - log with telemetry for investigation
            LogError(ex, CredentialOperation.Retrieve, resource,
                "Unexpected error retrieving credential for {Resource}. HResult: {HResult}",
                ex.HResult.ToString("X8"));
        }

        return null;
    }

    /// <summary>
    /// Retrieves an API key from the Windows Credential Manager.
    /// </summary>
    public string? GetApiKey(string resource)
    {
        var credential = GetCredential(resource);
        return credential?.Password;
    }

    /// <summary>
    /// Removes a credential from the Windows Credential Manager.
    /// </summary>
    public void RemoveCredential(string resource)
    {
        try
        {
            var credentials = _vault.FindAllByResource(resource);
            foreach (var credential in credentials)
            {
                _vault.Remove(credential);
            }
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070490)) // Element not found
        {
            // Credential not found - nothing to remove, this is fine
            LogDebug("Credential not found during removal for {Resource}", resource);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Access denied - credential exists but we can't remove it
            LogWarning(ex, CredentialOperation.Remove, resource,
                "Access denied when removing credential for {Resource}");
        }
        catch (Exception ex)
        {
            // Unexpected error during removal
            LogError(ex, CredentialOperation.Remove, resource,
                "Failed to remove credential for {Resource}");
        }
    }

    /// <summary>
    /// Checks if a credential exists in the Windows Credential Manager.
    /// </summary>
    public bool HasCredential(string resource)
    {
        try
        {
            var credentials = _vault.FindAllByResource(resource);
            return credentials.Count > 0;
        }
        catch (Exception ex)
        {
            // Log for diagnostic purposes - helps debug credential issues
            LogDebug("HasCredential check failed for {Resource}: {ExceptionType} - {Message}",
                resource, ex.GetType().Name, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets all stored credential resource names for this application.
    /// </summary>
    public IReadOnlyList<string> GetAllStoredResources()
    {
        var resources = new List<string>();
        try
        {
            var allCredentials = _vault.RetrieveAll();
            foreach (var credential in allCredentials)
            {
                if (credential.Resource.StartsWith(ResourcePrefix))
                {
                    if (!resources.Contains(credential.Resource))
                    {
                        resources.Add(credential.Resource);
                    }
                }
            }
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070490)) // Element not found
        {
            // Empty vault - no credentials stored yet
            LogDebug("No credentials found in vault (empty vault)");
        }
        catch (UnauthorizedAccessException ex)
        {
            // Access denied to vault
            LogWarning(ex, CredentialOperation.ListAll, null,
                "Access denied when listing all credentials from vault");
        }
        catch (Exception ex)
        {
            // Unexpected vault access failure - track for telemetry
            LogError(ex, CredentialOperation.ListAll, null,
                "Failed to list credentials from vault. HResult: {HResult}",
                ex.HResult.ToString("X8"));
        }

        return resources;
    }

    /// <summary>
    /// Removes all stored credentials for this application.
    /// </summary>
    public void RemoveAllCredentials()
    {
        var resources = GetAllStoredResources();
        foreach (var resource in resources)
        {
            RemoveCredential(resource);
        }
    }

    #region Alpaca-specific helpers

    /// <summary>
    /// Prompts for and optionally saves Alpaca API credentials.
    /// </summary>
    public async Task<(string KeyId, string SecretKey)?> PromptForAlpacaCredentialsAsync()
    {
        var result = await PromptForCredentialsAsync(
            AlpacaCredentialResource,
            "Enter your Alpaca API credentials.\n\nUsername: API Key ID\nPassword: Secret Key",
            "Alpaca API Credentials",
            CredentialSaveOption.Selected);

        if (result != null && !string.IsNullOrEmpty(result.CredentialUserName))
        {
            var keyId = result.CredentialUserName;
            var secretKey = result.CredentialPassword;

            if (result.CredentialSaved)
            {
                SaveCredential(AlpacaCredentialResource, keyId, secretKey);
            }

            return (keyId, secretKey);
        }

        return null;
    }

    /// <summary>
    /// Gets stored Alpaca credentials.
    /// </summary>
    public (string KeyId, string SecretKey)? GetAlpacaCredentials()
    {
        var credential = GetCredential(AlpacaCredentialResource);
        if (credential.HasValue)
        {
            return (credential.Value.Username, credential.Value.Password);
        }
        return null;
    }

    /// <summary>
    /// Saves Alpaca credentials.
    /// </summary>
    public void SaveAlpacaCredentials(string keyId, string secretKey)
    {
        SaveCredential(AlpacaCredentialResource, keyId, secretKey);
    }

    /// <summary>
    /// Checks if Alpaca credentials are stored.
    /// </summary>
    public bool HasAlpacaCredentials()
    {
        return HasCredential(AlpacaCredentialResource);
    }

    /// <summary>
    /// Removes stored Alpaca credentials.
    /// </summary>
    public void RemoveAlpacaCredentials()
    {
        RemoveCredential(AlpacaCredentialResource);
    }

    #endregion

    #region Nasdaq Data Link helpers

    /// <summary>
    /// Prompts for Nasdaq Data Link API key.
    /// </summary>
    public async Task<string?> PromptForNasdaqApiKeyAsync()
    {
        return await PromptForApiKeyAsync(
            NasdaqApiKeyResource,
            "Enter your Nasdaq Data Link (Quandl) API key.\n\nThis enables access to premium financial datasets.",
            "Nasdaq Data Link API Key");
    }

    /// <summary>
    /// Gets stored Nasdaq API key.
    /// </summary>
    public string? GetNasdaqApiKey()
    {
        return GetApiKey(NasdaqApiKeyResource);
    }

    /// <summary>
    /// Saves Nasdaq API key.
    /// </summary>
    public void SaveNasdaqApiKey(string apiKey)
    {
        SaveApiKey(NasdaqApiKeyResource, apiKey);
    }

    #endregion

    #region OpenFIGI helpers

    /// <summary>
    /// Prompts for OpenFIGI API key.
    /// </summary>
    public async Task<string?> PromptForOpenFigiApiKeyAsync()
    {
        return await PromptForApiKeyAsync(
            OpenFigiApiKeyResource,
            "Enter your OpenFIGI API key (optional).\n\nThis provides higher rate limits for symbol resolution.",
            "OpenFIGI API Key");
    }

    /// <summary>
    /// Gets stored OpenFIGI API key.
    /// </summary>
    public string? GetOpenFigiApiKey()
    {
        return GetApiKey(OpenFigiApiKeyResource);
    }

    /// <summary>
    /// Saves OpenFIGI API key.
    /// </summary>
    public void SaveOpenFigiApiKey(string apiKey)
    {
        SaveApiKey(OpenFigiApiKeyResource, apiKey);
    }

    #endregion

    #region Metadata Management

    /// <summary>
    /// Loads credential metadata from persistent storage.
    /// </summary>
    private async Task LoadMetadataAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_metadataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(_metadataPath))
            {
                var json = await File.ReadAllTextAsync(_metadataPath);
                var metadata = JsonSerializer.Deserialize<Dictionary<string, CredentialMetadata>>(json);
                if (metadata != null)
                {
                    _metadataCache = metadata;
                }
            }
        }
        catch (Exception)
        {
            _metadataCache = new Dictionary<string, CredentialMetadata>();
        }
    }

    /// <summary>
    /// Saves credential metadata to persistent storage.
    /// </summary>
    private async Task SaveMetadataAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_metadataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_metadataCache, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_metadataPath, json);
        }
        catch (Exception)
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Gets metadata for a specific credential.
    /// </summary>
    public CredentialMetadata? GetMetadata(string resource)
    {
        return _metadataCache.TryGetValue(resource, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Updates metadata for a specific credential.
    /// </summary>
    public async Task UpdateMetadataAsync(string resource, Action<CredentialMetadata> update)
    {
        if (!_metadataCache.TryGetValue(resource, out var metadata))
        {
            metadata = new CredentialMetadata { Resource = resource };
            _metadataCache[resource] = metadata;
        }

        update(metadata);
        await SaveMetadataAsync();

        MetadataUpdated?.Invoke(this, new CredentialMetadataEventArgs(resource, metadata));

        // Check for expiration warnings
        if (metadata.ExpiresAt.HasValue)
        {
            var remaining = metadata.ExpiresAt.Value - DateTime.UtcNow;
            if (remaining.TotalDays <= 7 && remaining.TotalSeconds > 0)
            {
                CredentialExpiring?.Invoke(this, new CredentialExpirationEventArgs(resource, metadata.ExpiresAt.Value));
            }
        }
    }

    /// <summary>
    /// Records a successful authentication for a credential.
    /// </summary>
    public async Task RecordAuthenticationAsync(string resource)
    {
        await UpdateMetadataAsync(resource, m =>
        {
            m.LastAuthenticatedAt = DateTime.UtcNow;
            m.TestStatus = CredentialTestStatus.Success;
        });
    }

    /// <summary>
    /// Gets extended credential info with metadata for all stored credentials.
    /// </summary>
    public List<Models.CredentialInfo> GetAllCredentialsWithMetadata()
    {
        var credentials = new List<Models.CredentialInfo>();
        var resources = GetAllStoredResources();

        foreach (var resource in resources)
        {
            var metadata = GetMetadata(resource);
            var (name, credType) = GetCredentialDisplayInfo(resource);

            credentials.Add(new Models.CredentialInfo
            {
                Name = name,
                Resource = resource,
                Status = GetCredentialStatusDisplay(resource, metadata),
                CredentialType = credType,
                ExpiresAt = metadata?.ExpiresAt,
                LastAuthenticatedAt = metadata?.LastAuthenticatedAt,
                LastTestedAt = metadata?.LastTestedAt,
                TestStatus = metadata?.TestStatus ?? CredentialTestStatus.Unknown,
                CanAutoRefresh = metadata?.AutoRefreshEnabled ?? false,
                RefreshToken = metadata?.RefreshToken
            });
        }

        return credentials;
    }

    private (string Name, CredentialType Type) GetCredentialDisplayInfo(string resource)
    {
        return resource switch
        {
            var r when r.Contains("Alpaca") => ("Alpaca API Credentials", CredentialType.ApiKeyWithSecret),
            var r when r.Contains("NasdaqDataLink") => ("Nasdaq Data Link API Key", CredentialType.ApiKey),
            var r when r.Contains("OpenFigi") => ("OpenFIGI API Key", CredentialType.ApiKey),
            var r when r.Contains("OAuth") => ("OAuth Token", CredentialType.OAuth2Token),
            _ => (resource.Replace(ResourcePrefix + ".", ""), CredentialType.ApiKey)
        };
    }

    private string GetCredentialStatusDisplay(string resource, CredentialMetadata? metadata)
    {
        if (metadata == null)
            return "Active";

        if (metadata.ExpiresAt.HasValue)
        {
            if (metadata.ExpiresAt.Value <= DateTime.UtcNow)
                return "Expired";
            if (metadata.ExpiresAt.Value <= DateTime.UtcNow.AddDays(1))
                return "Expires soon";
        }

        if (metadata.LastAuthenticatedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - metadata.LastAuthenticatedAt.Value;
            if (elapsed.TotalHours < 1)
                return "Active - Just used";
            if (elapsed.TotalHours < 24)
                return $"Active - Used {(int)elapsed.TotalHours}h ago";
            return $"Active - Used {(int)elapsed.TotalDays}d ago";
        }

        return "Active";
    }

    /// <summary>
    /// Gets credentials that are expiring within the specified number of days.
    /// </summary>
    public List<Models.CredentialInfo> GetExpiringCredentials(int withinDays = 7)
    {
        return GetAllCredentialsWithMetadata()
            .Where(c => c.IsExpiringSoon || c.IsExpired)
            .ToList();
    }

    #endregion

    #region Credential Testing

    /// <summary>
    /// Tests Alpaca credentials by making an authenticated API call.
    /// </summary>
    public async Task<CredentialTestResult> TestAlpacaCredentialsAsync(bool useSandbox = false)
    {
        var credentials = GetAlpacaCredentials();
        if (credentials == null)
        {
            return CredentialTestResult.CreateFailure("No Alpaca credentials stored");
        }

        return await TestAlpacaCredentialsAsync(credentials.Value.KeyId, credentials.Value.SecretKey, useSandbox);
    }

    /// <summary>
    /// Tests specific Alpaca credentials by making an authenticated API call.
    /// </summary>
    public async Task<CredentialTestResult> TestAlpacaCredentialsAsync(string keyId, string secretKey, bool useSandbox = false)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var baseUrl = useSandbox ? AlpacaPaperBaseUrl : AlpacaLiveBaseUrl;
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v2/account");
            request.Headers.Add("APCA-API-KEY-ID", keyId);
            request.Headers.Add("APCA-API-SECRET-KEY", secretKey);

            var response = await _httpClient.SendAsync(request);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var account = JsonSerializer.Deserialize<JsonElement>(content);

                await UpdateMetadataAsync(AlpacaCredentialResource, m =>
                {
                    m.LastTestedAt = DateTime.UtcNow;
                    m.LastAuthenticatedAt = DateTime.UtcNow;
                    m.TestStatus = CredentialTestStatus.Success;
                    m.CredentialType = CredentialType.ApiKeyWithSecret;
                });

                var accountStatus = account.TryGetProperty("status", out var status) ? status.GetString() : "Unknown";
                return new CredentialTestResult
                {
                    Success = true,
                    Message = $"Authentication successful. Account status: {accountStatus}",
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    TestedAt = DateTime.UtcNow,
                    ServerInfo = useSandbox ? "Alpaca Paper Trading" : "Alpaca Live Trading"
                };
            }

            var errorMessage = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Invalid API key or secret",
                System.Net.HttpStatusCode.Forbidden => "API key does not have required permissions",
                _ => $"API returned {(int)response.StatusCode}: {response.ReasonPhrase}"
            };

            await UpdateMetadataAsync(AlpacaCredentialResource, m =>
            {
                m.LastTestedAt = DateTime.UtcNow;
                m.TestStatus = CredentialTestStatus.Failed;
            });

            return CredentialTestResult.CreateFailure(errorMessage);
        }
        catch (TaskCanceledException)
        {
            return CredentialTestResult.CreateFailure("Connection timed out");
        }
        catch (HttpRequestException ex)
        {
            return CredentialTestResult.CreateFailure($"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CredentialTestResult.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests Nasdaq Data Link API key.
    /// </summary>
    public async Task<CredentialTestResult> TestNasdaqApiKeyAsync()
    {
        var apiKey = GetNasdaqApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return CredentialTestResult.CreateFailure("No Nasdaq Data Link API key stored");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            // Test with a simple dataset query
            var testUrl = $"https://data.nasdaq.com/api/v3/datasets.json?api_key={apiKey}&per_page=1";
            var response = await _httpClient.GetAsync(testUrl);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                await UpdateMetadataAsync(NasdaqApiKeyResource, m =>
                {
                    m.LastTestedAt = DateTime.UtcNow;
                    m.LastAuthenticatedAt = DateTime.UtcNow;
                    m.TestStatus = CredentialTestStatus.Success;
                    m.CredentialType = CredentialType.ApiKey;
                });

                return new CredentialTestResult
                {
                    Success = true,
                    Message = "API key validated successfully",
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    TestedAt = DateTime.UtcNow,
                    ServerInfo = "Nasdaq Data Link"
                };
            }

            var errorMessage = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Invalid API key",
                System.Net.HttpStatusCode.Forbidden => "API key does not have required permissions",
                (System.Net.HttpStatusCode)429 => "Rate limit exceeded",
                _ => $"API returned {(int)response.StatusCode}"
            };

            await UpdateMetadataAsync(NasdaqApiKeyResource, m =>
            {
                m.LastTestedAt = DateTime.UtcNow;
                m.TestStatus = CredentialTestStatus.Failed;
            });

            return CredentialTestResult.CreateFailure(errorMessage);
        }
        catch (Exception ex)
        {
            return CredentialTestResult.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests OpenFIGI API key.
    /// </summary>
    public async Task<CredentialTestResult> TestOpenFigiApiKeyAsync()
    {
        var apiKey = GetOpenFigiApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return CredentialTestResult.CreateFailure("No OpenFIGI API key stored");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openfigi.com/v3/mapping");
            request.Headers.Add("X-OPENFIGI-APIKEY", apiKey);
            request.Content = new StringContent(
                "[{\"idType\":\"TICKER\",\"idValue\":\"AAPL\"}]",
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                await UpdateMetadataAsync(OpenFigiApiKeyResource, m =>
                {
                    m.LastTestedAt = DateTime.UtcNow;
                    m.LastAuthenticatedAt = DateTime.UtcNow;
                    m.TestStatus = CredentialTestStatus.Success;
                    m.CredentialType = CredentialType.ApiKey;
                });

                return new CredentialTestResult
                {
                    Success = true,
                    Message = "API key validated successfully",
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    TestedAt = DateTime.UtcNow,
                    ServerInfo = "OpenFIGI API v3"
                };
            }

            await UpdateMetadataAsync(OpenFigiApiKeyResource, m =>
            {
                m.LastTestedAt = DateTime.UtcNow;
                m.TestStatus = CredentialTestStatus.Failed;
            });

            return CredentialTestResult.CreateFailure($"API returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return CredentialTestResult.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests a credential by its resource name.
    /// </summary>
    public async Task<CredentialTestResult> TestCredentialAsync(string resource)
    {
        return resource switch
        {
            var r when r.Contains("Alpaca") => await TestAlpacaCredentialsAsync(),
            var r when r.Contains("NasdaqDataLink") => await TestNasdaqApiKeyAsync(),
            var r when r.Contains("OpenFigi") => await TestOpenFigiApiKeyAsync(),
            _ => CredentialTestResult.CreateFailure($"No test available for {resource}")
        };
    }

    /// <summary>
    /// Tests all stored credentials and returns results.
    /// </summary>
    public async Task<Dictionary<string, CredentialTestResult>> TestAllCredentialsAsync()
    {
        var results = new Dictionary<string, CredentialTestResult>();
        var resources = GetAllStoredResources();

        foreach (var resource in resources)
        {
            results[resource] = await TestCredentialAsync(resource);
        }

        return results;
    }

    #endregion

    #region OAuth Token Management

    /// <summary>
    /// Saves an OAuth token with metadata for expiration tracking.
    /// </summary>
    public async Task SaveOAuthTokenAsync(
        string providerId,
        string accessToken,
        string? refreshToken,
        DateTime expiresAt,
        string? tokenEndpoint = null,
        string? clientId = null)
    {
        var resource = $"{OAuthTokenResource}.{providerId}";
        SaveCredential(resource, "oauth", accessToken);

        await UpdateMetadataAsync(resource, m =>
        {
            m.CredentialType = CredentialType.OAuth2Token;
            m.ExpiresAt = expiresAt;
            m.RefreshToken = refreshToken;
            m.TokenEndpoint = tokenEndpoint;
            m.ClientId = clientId;
            m.AutoRefreshEnabled = !string.IsNullOrEmpty(refreshToken);
            m.CreatedAt = DateTime.UtcNow;
        });
    }

    /// <summary>
    /// Gets an OAuth access token for a provider.
    /// </summary>
    public string? GetOAuthToken(string providerId)
    {
        var resource = $"{OAuthTokenResource}.{providerId}";
        var credential = GetCredential(resource);
        return credential?.Password;
    }

    /// <summary>
    /// Refreshes an OAuth token using the stored refresh token.
    /// </summary>
    public async Task<bool> RefreshOAuthTokenAsync(string providerId)
    {
        var resource = $"{OAuthTokenResource}.{providerId}";
        var metadata = GetMetadata(resource);

        if (metadata == null || string.IsNullOrEmpty(metadata.RefreshToken) ||
            string.IsNullOrEmpty(metadata.TokenEndpoint))
        {
            return false;
        }

        try
        {
            await UpdateMetadataAsync(resource, m => m.LastRefreshAttemptAt = DateTime.UtcNow);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = metadata.RefreshToken,
                ["client_id"] = metadata.ClientId ?? ""
            });

            var response = await _httpClient.PostAsync(metadata.TokenEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(json);

                if (tokenResponse != null)
                {
                    await SaveOAuthTokenAsync(
                        providerId,
                        tokenResponse.AccessToken,
                        tokenResponse.RefreshToken ?? metadata.RefreshToken,
                        tokenResponse.GetExpirationTime(),
                        metadata.TokenEndpoint,
                        metadata.ClientId);

                    await UpdateMetadataAsync(resource, m =>
                    {
                        m.RefreshFailureCount = 0;
                        m.LastAuthenticatedAt = DateTime.UtcNow;
                    });

                    return true;
                }
            }

            await UpdateMetadataAsync(resource, m => m.RefreshFailureCount++);
            return false;
        }
        catch (Exception)
        {
            await UpdateMetadataAsync(resource, m => m.RefreshFailureCount++);
            return false;
        }
    }

    /// <summary>
    /// Checks if an OAuth token needs refresh and refreshes if necessary.
    /// </summary>
    public async Task<bool> EnsureTokenValidAsync(string providerId, int refreshThresholdMinutes = 5)
    {
        var resource = $"{OAuthTokenResource}.{providerId}";
        var metadata = GetMetadata(resource);

        if (metadata == null)
            return false;

        if (!metadata.ExpiresAt.HasValue)
            return true; // No expiration, assume valid

        var remaining = metadata.ExpiresAt.Value - DateTime.UtcNow;
        if (remaining.TotalMinutes > refreshThresholdMinutes)
            return true; // Token still valid

        if (remaining.TotalSeconds <= 0)
        {
            // Token expired, try to refresh
            if (metadata.AutoRefreshEnabled)
            {
                return await RefreshOAuthTokenAsync(providerId);
            }
            return false;
        }

        // Token expiring soon, proactively refresh if enabled
        if (metadata.AutoRefreshEnabled)
        {
            return await RefreshOAuthTokenAsync(providerId);
        }

        return true; // Still valid but will expire soon
    }

    #endregion

    #region Logging Helpers

    /// <summary>
    /// Logs a debug message with structured parameters.
    /// </summary>
    private void LogDebug(string messageTemplate, params object[] args)
    {
        var message = FormatMessage(messageTemplate, args);
        Debug.WriteLine($"{LogPrefix} [DEBUG] {message}");
    }

    /// <summary>
    /// Logs a warning with exception details.
    /// </summary>
    private void LogWarning(Exception ex, CredentialOperation operation, string? resource, string messageTemplate, params object[] args)
    {
        var message = FormatMessage(messageTemplate, args);
        Debug.WriteLine($"{LogPrefix} [WARN] {message} | Operation: {operation} | Resource: {resource ?? "N/A"} | Exception: {ex.GetType().Name}");

        RaiseCredentialError(operation, resource, message, ex, CredentialErrorSeverity.Warning);
    }

    /// <summary>
    /// Logs an error with exception details and raises the CredentialError event.
    /// </summary>
    private void LogError(Exception ex, CredentialOperation operation, string? resource, string messageTemplate, params object[] args)
    {
        var message = FormatMessage(messageTemplate, args);
        Debug.WriteLine($"{LogPrefix} [ERROR] {message} | Operation: {operation} | Resource: {resource ?? "N/A"} | Exception: {ex.GetType().Name} - {ex.Message}");

        RaiseCredentialError(operation, resource, message, ex, CredentialErrorSeverity.Error);
    }

    /// <summary>
    /// Formats a message template with arguments, similar to structured logging.
    /// </summary>
    private static string FormatMessage(string template, object[] args)
    {
        if (args.Length == 0) return template;

        try
        {
            // Simple placeholder replacement for {Name} style templates
            var result = template;
            var index = 0;
            while (result.Contains('{') && index < args.Length)
            {
                var start = result.IndexOf('{');
                var end = result.IndexOf('}', start);
                if (end > start)
                {
                    result = result[..start] + (args[index]?.ToString() ?? "null") + result[(end + 1)..];
                    index++;
                }
                else break;
            }
            return result;
        }
        catch
        {
            return template;
        }
    }

    /// <summary>
    /// Raises the CredentialError event with the specified details.
    /// </summary>
    private void RaiseCredentialError(CredentialOperation operation, string? resource, string message, Exception? exception, CredentialErrorSeverity severity)
    {
        CredentialError?.Invoke(this, new CredentialErrorEventArgs(
            operation,
            resource,
            message,
            exception,
            severity,
            DateTime.UtcNow));
    }

    #endregion
}

/// <summary>
/// Event args for credential metadata updates.
/// </summary>
public class CredentialMetadataEventArgs : EventArgs
{
    public string Resource { get; }
    public CredentialMetadata Metadata { get; }

    public CredentialMetadataEventArgs(string resource, CredentialMetadata metadata)
    {
        Resource = resource;
        Metadata = metadata;
    }
}

/// <summary>
/// Event args for credential expiration warnings.
/// </summary>
public class CredentialExpirationEventArgs : EventArgs
{
    public string Resource { get; }
    public DateTime ExpiresAt { get; }
    public TimeSpan TimeRemaining => ExpiresAt - DateTime.UtcNow;

    public CredentialExpirationEventArgs(string resource, DateTime expiresAt)
    {
        Resource = resource;
        ExpiresAt = expiresAt;
    }
}

/// <summary>
/// Event args for credential operation errors.
/// </summary>
public class CredentialErrorEventArgs : EventArgs
{
    /// <summary>
    /// The operation that failed.
    /// </summary>
    public CredentialOperation Operation { get; }

    /// <summary>
    /// The resource (credential name) involved, if applicable.
    /// </summary>
    public string? Resource { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The exception that caused the error, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Severity level of the error.
    /// </summary>
    public CredentialErrorSeverity Severity { get; }

    /// <summary>
    /// When the error occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    public CredentialErrorEventArgs(
        CredentialOperation operation,
        string? resource,
        string message,
        Exception? exception,
        CredentialErrorSeverity severity,
        DateTime timestamp)
    {
        Operation = operation;
        Resource = resource;
        Message = message;
        Exception = exception;
        Severity = severity;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Types of credential operations that can fail.
/// </summary>
public enum CredentialOperation
{
    /// <summary>Prompting user for credentials.</summary>
    PromptCredentials,

    /// <summary>Prompting user for API key.</summary>
    PromptApiKey,

    /// <summary>Saving credential to vault.</summary>
    Save,

    /// <summary>Retrieving credential from vault.</summary>
    Retrieve,

    /// <summary>Removing credential from vault.</summary>
    Remove,

    /// <summary>Listing all credentials.</summary>
    ListAll,

    /// <summary>Testing credential validity.</summary>
    Test,

    /// <summary>Refreshing OAuth token.</summary>
    Refresh
}

/// <summary>
/// Severity levels for credential errors.
/// </summary>
public enum CredentialErrorSeverity
{
    /// <summary>Informational - operation failed but not critical.</summary>
    Info,

    /// <summary>Warning - operation failed, may require user attention.</summary>
    Warning,

    /// <summary>Error - operation failed, likely requires user action.</summary>
    Error,

    /// <summary>Critical - security-related failure, immediate attention required.</summary>
    Critical
}
