using BCDev.Models;
using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class LaunchConfigServiceTests
{
    private readonly LaunchConfigService _service;
    private readonly string _fixturesPath;

    public LaunchConfigServiceTests()
    {
        _service = new LaunchConfigService();
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    }

    [Fact]
    public void LoadLaunchJson_ValidFile_ReturnsConfigurations()
    {
        var path = Path.Combine(_fixturesPath, "valid-launch.json");

        var result = _service.LoadLaunchJson(path);

        Assert.NotNull(result);
        Assert.Equal(2, result.Configurations.Count);
    }

    [Fact]
    public void LoadLaunchJson_FileNotFound_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_fixturesPath, "nonexistent.json");

        Assert.Throws<FileNotFoundException>(() => _service.LoadLaunchJson(path));
    }

    [Fact]
    public void GetConfiguration_ValidName_ReturnsConfiguration()
    {
        var path = Path.Combine(_fixturesPath, "valid-launch.json");

        var result = _service.GetConfiguration(path, "test_config");

        Assert.NotNull(result);
        Assert.Equal("test_config", result.Name);
        Assert.Equal("https://localhost", result.Server);
        Assert.Equal(7049, result.Port);
        Assert.Equal("BC", result.ServerInstance);
        Assert.Equal("default", result.Tenant);
        Assert.Equal(AuthenticationMethod.UserPassword, result.Authentication);
    }

    [Fact]
    public void GetConfiguration_AadConfig_ReturnsCorrectAuthMethod()
    {
        var path = Path.Combine(_fixturesPath, "valid-launch.json");

        var result = _service.GetConfiguration(path, "aad_config");

        Assert.Equal(AuthenticationMethod.AAD, result.Authentication);
    }

    [Fact]
    public void GetConfiguration_InvalidName_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_fixturesPath, "valid-launch.json");

        var ex = Assert.Throws<InvalidOperationException>(
            () => _service.GetConfiguration(path, "nonexistent_config"));

        Assert.Contains("not found", ex.Message);
        Assert.Contains("test_config", ex.Message); // Should list available configs
    }

    [Fact]
    public void GetConfiguration_CaseInsensitive_ReturnsConfiguration()
    {
        var path = Path.Combine(_fixturesPath, "valid-launch.json");

        var result = _service.GetConfiguration(path, "TEST_CONFIG");

        Assert.NotNull(result);
        Assert.Equal("test_config", result.Name);
    }
}
