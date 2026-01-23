namespace BCDev.Models;

/// <summary>
/// Authentication types supported by the BC CLI
/// </summary>
public enum AuthType
{
    /// <summary>
    /// Username and password authentication (NavUserPassword)
    /// </summary>
    UserPassword,

    /// <summary>
    /// Azure Active Directory / Microsoft Entra ID authentication
    /// </summary>
    AAD
}
