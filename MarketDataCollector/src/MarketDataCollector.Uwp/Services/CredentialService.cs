using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;

namespace MarketDataCollector.Uwp.Services;

/// <summary>
/// Service for secure credential management using Windows Credential Manager
/// and CredentialPicker UI.
/// </summary>
public class CredentialService
{
    private const string ResourcePrefix = "MarketDataCollector";

    // Credential resource names
    public const string AlpacaCredentialResource = $"{ResourcePrefix}.Alpaca";
    public const string NasdaqApiKeyResource = $"{ResourcePrefix}.NasdaqDataLink";
    public const string OpenFigiApiKeyResource = $"{ResourcePrefix}.OpenFigi";

    private readonly PasswordVault _vault;

    public CredentialService()
    {
        _vault = new PasswordVault();
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
        catch (Exception)
        {
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
        catch (Exception)
        {
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
        catch (Exception)
        {
            // Ignore errors when saving
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
        catch (Exception)
        {
            // Credential not found or access denied
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
        catch (Exception)
        {
            // Credential not found or access denied
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
        catch (Exception)
        {
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
        catch (Exception)
        {
            // Access denied or empty vault
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
}
