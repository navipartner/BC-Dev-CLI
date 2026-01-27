using BCDev.Models;
using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class SymbolServiceTests
{
    private readonly string _fixturesPath;

    public SymbolServiceTests()
    {
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    }

    [Fact]
    public void GetSymbolsToDownload_ReturnsApplicationSystemAndBaseApp()
    {
        var appJson = new AppJson
        {
            Platform = "27.0.0.0",
            Application = "27.0.0.0",
            Dependencies = new List<AppDependency>()
        };

        var symbols = SymbolService.GetSymbolsToDownload(appJson);

        Assert.Contains(symbols, s => s.Name == "Application" && s.Publisher == "Microsoft");
        Assert.Contains(symbols, s => s.Name == "System" && s.Publisher == "Microsoft");
        Assert.Contains(symbols, s => s.Name == "Base Application" && s.Publisher == "Microsoft");
    }

    [Fact]
    public void GetSymbolsToDownload_IncludesDependencies()
    {
        var appJson = new AppJson
        {
            Platform = "27.0.0.0",
            Application = "27.0.0.0",
            Dependencies = new List<AppDependency>
            {
                new() { Id = "test-id", Name = "Custom App", Publisher = "Custom", Version = "1.0.0.0" }
            }
        };

        var symbols = SymbolService.GetSymbolsToDownload(appJson);

        Assert.Contains(symbols, s => s.Name == "Custom App" && s.Publisher == "Custom");
    }

    [Fact]
    public void GetSymbolFileName_FormatsCorrectly()
    {
        var result = SymbolService.GetSymbolFileName("Microsoft", "Base Application", "27.0.0.0");

        Assert.Equal("Microsoft_Base Application_27.0.0.0.app", result);
    }

    [Fact]
    public void BuildPackageUrl_OnPrem_FormatsCorrectly()
    {
        var config = new LaunchConfiguration
        {
            Server = "https://localhost",
            Port = 7049,
            ServerInstance = "BC",
            Tenant = "default"
        };

        var url = SymbolService.BuildPackageUrl(config, "Microsoft", "Application", "27.0.0.0", null);

        Assert.Contains("/dev/packages?", url);
        Assert.Contains("publisher=Microsoft", url);
        Assert.Contains("appName=Application", url);
        Assert.Contains("versionText=27.0.0.0", url);
        Assert.Contains("tenant=default", url);
    }

    [Fact]
    public void BuildPackageUrl_WithAppId_IncludesAppId()
    {
        var config = new LaunchConfiguration
        {
            Server = "https://localhost",
            Port = 7049,
            ServerInstance = "BC",
            Tenant = "default"
        };

        var url = SymbolService.BuildPackageUrl(config, "Microsoft", "Base Application", "27.0.0.0", "437dbf0e-84ff-417a-965d-ed2bb9650972");

        Assert.Contains("appId=437dbf0e-84ff-417a-965d-ed2bb9650972", url);
    }
}
