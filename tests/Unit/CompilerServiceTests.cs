using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class CompilerServiceTests
{
    private readonly CompilerService _service;

    public CompilerServiceTests()
    {
        _service = new CompilerService();
    }

    [Fact]
    public async Task CompileAsync_AppJsonNotFound_ReturnsFailure()
    {
        var result = await _service.CompileAsync(
            "/nonexistent/path/app.json",
            "/some/compiler/path",
            null);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task CompileAsync_CompilerNotFound_ReturnsFailure()
    {
        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var appJsonPath = Path.Combine(fixturesPath, "valid-app.json");

        var result = await _service.CompileAsync(
            appJsonPath,
            "/nonexistent/compiler/alc.exe",
            null);

        Assert.False(result.Success);
        Assert.Contains("Compiler not found", result.Message);
    }
}
