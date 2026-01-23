using System.Net;
using Microsoft.Identity.Client;

namespace BCDev.Auth;

/// <summary>
/// Credential provider for Azure Active Directory / Microsoft Entra ID authentication
/// </summary>
public class AadAuthProvider : ICredentialProvider
{
    private readonly string _clientId;
    private readonly string _authority;
    private readonly string[] _scopes;
    private readonly string? _username;
    private readonly string? _password;
    private string? _cachedToken;

    // Default client ID for BC client (Visual Studio Code AL Extension)
    private const string DefaultBCClientId = "a1d332c5-84b8-4d8f-85aa-345f987d53be";

    public AadAuthProvider(string authority, string[] scopes, string? clientId = null,
        string? username = null, string? password = null)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _clientId = clientId ?? DefaultBCClientId;
        _username = username;
        _password = password;
    }

    public string AuthenticationScheme => "AzureActiveDirectory";

    public async Task<ICredentials> GetCredentialsAsync()
    {
        var token = await AcquireTokenAsync();
        return new TokenCredential(token);
    }

    private async Task<string> AcquireTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedToken))
        {
            return _cachedToken;
        }

        var app = PublicClientApplicationBuilder
            .Create(_clientId)
            .WithAuthority(_authority)
            .WithDefaultRedirectUri()
            .Build();

        AuthenticationResult result;

        // Try username/password flow if credentials provided (for CI/CD)
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            try
            {
                result = await app.AcquireTokenByUsernamePassword(_scopes, _username, _password)
                    .ExecuteAsync();
            }
            catch (MsalException ex)
            {
                throw new Exception($"AAD authentication failed: {ex.Message}", ex);
            }
        }
        else
        {
            // Interactive flow
            try
            {
                result = await app.AcquireTokenInteractive(_scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync();
            }
            catch (MsalException ex)
            {
                throw new Exception($"AAD interactive authentication failed: {ex.Message}", ex);
            }
        }

        _cachedToken = result.AccessToken;
        return _cachedToken;
    }
}

/// <summary>
/// Token-based credential for AAD authentication
/// </summary>
public class TokenCredential : ICredentials
{
    private readonly string _token;

    public TokenCredential(string token)
    {
        _token = token;
    }

    public NetworkCredential GetCredential(Uri uri, string authType)
    {
        // Return the token as the password with an empty username
        // BC's JsonHttpClient will use this for bearer token auth
        return new NetworkCredential(string.Empty, _token);
    }
}
