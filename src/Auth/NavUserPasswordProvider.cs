using System.Net;

namespace BCDev.Auth;

/// <summary>
/// Credential provider for NavUserPassword (username/password) authentication
/// </summary>
public class NavUserPasswordProvider : ICredentialProvider
{
    private readonly string _username;
    private readonly string _password;

    public NavUserPasswordProvider(string username, string password)
    {
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
    }

    public string AuthenticationScheme => "UserNamePassword";

    public Task<ICredentials> GetCredentialsAsync()
    {
        var credential = new NetworkCredential(_username, _password);
        return Task.FromResult<ICredentials>(credential);
    }
}
