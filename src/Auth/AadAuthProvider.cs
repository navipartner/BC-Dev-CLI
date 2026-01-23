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

    private const string DefaultBCClientId = ""; // TODO

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

        // Use device code flow - works across tenants without explicit app consent
        try
        {
            Console.Error.WriteLine("Authenticating with Microsoft Entra ID...");
                result = await app.AcquireTokenWithDeviceCode(_scopes, deviceCodeResult =>
                {
                    Console.Error.WriteLine(deviceCodeResult.Message);
                    return Task.CompletedTask;
                }).ExecuteAsync();
            }
            catch (MsalException ex)
            {
            throw new Exception($"AAD device code authentication failed: {ex.Message}", ex);
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
