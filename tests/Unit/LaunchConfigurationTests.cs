using BCDev.Models;
using Xunit;

namespace BCDev.Tests.Unit;

public class LaunchConfigurationTests
{
    [Fact]
    public void GetClientServicesUrl_HttpsServer_ReturnsCorrectUrl()
    {
        var config = new LaunchConfiguration
        {
            Server = "https://myserver.com",
            Port = 443,
            ServerInstance = "BC",
            Tenant = "default"
        };

        var result = config.GetClientServicesUrl();

        Assert.Equal("https://myserver.com:443/BC?tenant=default", result);
    }

    [Fact]
    public void GetClientServicesUrl_HttpServer_ReturnsCorrectUrl()
    {
        var config = new LaunchConfiguration
        {
            Server = "http://localhost",
            Port = 7049,
            ServerInstance = "BC",
            Tenant = "mytenant"
        };

        var result = config.GetClientServicesUrl();

        Assert.Equal("http://localhost:7049/BC?tenant=mytenant", result);
    }

    [Fact]
    public void GetClientServicesUrl_EmptyTenant_UsesDefaultTenant()
    {
        var config = new LaunchConfiguration
        {
            Server = "https://myserver.com",
            Port = 443,
            ServerInstance = "BC",
            Tenant = ""
        };

        var result = config.GetClientServicesUrl();

        // Implementation uses "default" as fallback when tenant is empty
        Assert.Equal("https://myserver.com:443/BC?tenant=default", result);
    }

    [Fact]
    public void GetClientServicesUrl_TrailingSlash_HandlesCorrectly()
    {
        var config = new LaunchConfiguration
        {
            Server = "https://myserver.com/",
            Port = 443,
            ServerInstance = "BC",
            Tenant = "default"
        };

        var result = config.GetClientServicesUrl();

        Assert.Equal("https://myserver.com:443/BC?tenant=default", result);
    }

    [Fact]
    public void SchemaUpdateMode_DefaultsToSynchronize()
    {
        var config = new LaunchConfiguration();

        Assert.Equal(SchemaUpdateMode.Synchronize, config.SchemaUpdateMode);
    }
}
