using System.Runtime.InteropServices;
using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Integration;

public class CompileIntegrationTests
{
    private readonly CompilerService _service;
    private readonly string _fixturesPath;
    private readonly string _toolsPath;

    public CompileIntegrationTests()
    {
        _service = new CompilerService();
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        _toolsPath = Path.Combine(AppContext.BaseDirectory, "Tools", "alc");
    }

    private string GetCompilerPath()
    {
        // The alc compiler is platform-specific
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(_toolsPath, "alc");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Path.Combine(_toolsPath, "alc");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(_toolsPath, "alc.exe");
        }

        throw new PlatformNotSupportedException("Unsupported platform for AL compiler");
    }

    [Fact]
    public async Task CompileAsync_SimpleApp_Succeeds()
    {
        // Arrange
        var compilerPath = GetCompilerPath();
        if (!File.Exists(compilerPath))
        {
            // Skip if compiler not available (e.g., wrong platform binaries)
            return;
        }

        var testAppPath = Path.Combine(_fixturesPath, "SimpleTestApp");
        var appJsonPath = Path.Combine(testAppPath, "app.json");

        // Create a temporary copy to avoid polluting the fixtures
        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-test-{Guid.NewGuid()}");
        CopyDirectory(testAppPath, tempDir);
        var tempAppJsonPath = Path.Combine(tempDir, "app.json");

        try
        {
            // Act
            var result = await _service.CompileAsync(tempAppJsonPath, compilerPath, null);

            // Assert
            Assert.True(result.Success, $"Compilation failed: {result.Message}\nStdOut: {result.StdOut}\nStdErr: {result.StdErr}");
            Assert.NotNull(result.AppPath);
            Assert.True(File.Exists(result.AppPath), $"App file should exist at {result.AppPath}");
            Assert.Equal("TestPublisher_Simple_Test_App_1.0.0.0.app", Path.GetFileName(result.AppPath));
            Assert.Empty(result.Errors);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CompileAsync_WithPackageCachePath_UsesPath()
    {
        // Arrange
        var compilerPath = GetCompilerPath();
        if (!File.Exists(compilerPath))
        {
            return;
        }

        var testAppPath = Path.Combine(_fixturesPath, "SimpleTestApp");
        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-test-{Guid.NewGuid()}");
        CopyDirectory(testAppPath, tempDir);
        var tempAppJsonPath = Path.Combine(tempDir, "app.json");

        // Create an empty .alpackages folder
        var packageCachePath = Path.Combine(tempDir, ".alpackages");
        Directory.CreateDirectory(packageCachePath);

        try
        {
            // Act
            var result = await _service.CompileAsync(tempAppJsonPath, compilerPath, packageCachePath);

            // Assert - should succeed (simple app has no dependencies)
            Assert.True(result.Success, $"Compilation failed: {result.Message}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CompileAsync_InvalidAlCode_ReturnsErrors()
    {
        // Arrange
        var compilerPath = GetCompilerPath();
        if (!File.Exists(compilerPath))
        {
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "src"));

        // Create app.json
        var appJson = """
        {
          "id": "55555555-5555-5555-5555-555555555555",
          "name": "Invalid App",
          "publisher": "Test",
          "version": "1.0.0.0",
          "platform": "27.0.0.0",
          "runtime": "14.0",
          "target": "OnPrem",
          "idRanges": [{ "from": 50000, "to": 50099 }]
        }
        """;
        var appJsonPath = Path.Combine(tempDir, "app.json");
        await File.WriteAllTextAsync(appJsonPath, appJson);

        // Create invalid AL code
        var invalidAl = """
        codeunit 50000 "Invalid"
        {
            procedure Test()
            begin
                // Missing semicolon and invalid syntax
                x :=
            end;
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(tempDir, "src", "Invalid.Codeunit.al"), invalidAl);

        try
        {
            // Act
            var result = await _service.CompileAsync(appJsonPath, compilerPath, null);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}
