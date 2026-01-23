using System.Text.Json;
using BCDev.Models;
using Xunit;

namespace BCDev.Tests.Unit;

public class AppJsonParsingTests
{
    private readonly string _fixturesPath;

    public AppJsonParsingTests()
    {
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    }

    [Fact]
    public void ParseAppJson_ValidFile_ReturnsAppJson()
    {
        var path = Path.Combine(_fixturesPath, "valid-app.json");
        var json = File.ReadAllText(path);

        var result = JsonSerializer.Deserialize<AppJson>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(result);
        Assert.Equal("22222222-2222-2222-2222-222222222222", result.Id);
        Assert.Equal("Test App", result.Name);
        Assert.Equal("TestPublisher", result.Publisher);
        Assert.Equal("1.0.0.0", result.Version);
    }

    [Fact]
    public void ParseAppJson_WithDependencies_ParsesDependencies()
    {
        var path = Path.Combine(_fixturesPath, "app-with-dependencies.json");
        var json = File.ReadAllText(path);

        var result = JsonSerializer.Deserialize<AppJson>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(result);
        Assert.NotNull(result.Dependencies);
        Assert.Single(result.Dependencies);
        Assert.Equal("Base Application", result.Dependencies[0].Name);
        Assert.Equal("Microsoft", result.Dependencies[0].Publisher);
    }

    [Fact]
    public void GetAppFileName_ReturnsCorrectFormat()
    {
        var appJson = new AppJson
        {
            Publisher = "My Publisher",
            Name = "My App",
            Version = "1.2.3.4"
        };

        var result = appJson.GetAppFileName();

        Assert.Equal("My_Publisher_My_App_1.2.3.4.app", result);
    }

    [Fact]
    public void GetAppFileName_HandlesSpaces()
    {
        var appJson = new AppJson
        {
            Publisher = "Test Publisher",
            Name = "Test App Name",
            Version = "1.0.0.0"
        };

        var result = appJson.GetAppFileName();

        Assert.Equal("Test_Publisher_Test_App_Name_1.0.0.0.app", result);
    }
}
