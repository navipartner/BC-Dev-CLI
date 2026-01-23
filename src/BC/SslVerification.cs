using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace BCDev.BC;

/// <summary>
/// SSL certificate verification helper for development environments
/// </summary>
public static class SslVerification
{
    private static bool _disabled = false;

    private static bool ValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    /// <summary>
    /// Disable SSL certificate validation (for self-signed certs in dev environments)
    /// </summary>
    public static void Disable()
    {
        if (!_disabled)
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback = ValidationCallback;
            _disabled = true;
        }
    }

    /// <summary>
    /// Enable SSL certificate validation
    /// </summary>
    public static void Enable()
    {
        System.Net.ServicePointManager.ServerCertificateValidationCallback = null;
        _disabled = false;
    }
}
