using System.Net;

namespace BCDev.Auth;

/// <summary>
/// Interface for credential providers
/// </summary>
public interface ICredentialProvider
{
    /// <summary>
    /// Get credentials for authentication
    /// </summary>
    Task<ICredentials> GetCredentialsAsync();

    /// <summary>
    /// Get the authentication scheme name for BC client
    /// </summary>
    string AuthenticationScheme { get; }
}
