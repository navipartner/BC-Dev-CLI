using System.Runtime.InteropServices;
using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Integration;

public class WarningSuppressionIntegrationTests
{
    private readonly CompilerService _service;
    private readonly string _fixturesPath;
    private readonly string _toolsPath;

    public WarningSuppressionIntegrationTests()
    {
        _service = new CompilerService();
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        _toolsPath = Path.Combine(AppContext.BaseDirectory, "Tools", "alc");
    }

    private string GetCompilerPath()
    {
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
    public async Task CompileAsync_AppWithWarnings_SuppressWarningsFalse_ShowsWarningsInOutput()
    {
        // Arrange
        var compilerPath = GetCompilerPath();
        if (!File.Exists(compilerPath))
        {
            return; // Skip if compiler not available
        }

        var testAppPath = Path.Combine(_fixturesPath, "AppWithWarnings");
        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-test-{Guid.NewGuid()}");
        CopyDirectory(testAppPath, tempDir);
        var tempAppJsonPath = Path.Combine(tempDir, "app.json");

        try
        {
            // Act
            var result = await _service.CompileAsync(tempAppJsonPath, compilerPath, null, suppressWarnings: false);

            // Assert
            Assert.True(result.Success, $"Compilation should succeed: {result.Message}");

            // Warnings should be visible in raw output
            var combinedOutput = (result.StdOut ?? "") + (result.StdErr ?? "");
            Assert.Contains(": warning ", combinedOutput);
            Assert.Contains("AL0432", combinedOutput); // Obsolete warning code

            // Warnings should also be in the parsed list
            Assert.NotEmpty(result.Warnings);
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
    public async Task CompileAsync_AppWithWarnings_SuppressWarningsTrue_HidesWarningsFromOutput()
    {
        // Arrange
        var compilerPath = GetCompilerPath();
        if (!File.Exists(compilerPath))
        {
            return; // Skip if compiler not available
        }

        var testAppPath = Path.Combine(_fixturesPath, "AppWithWarnings");
        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-test-{Guid.NewGuid()}");
        CopyDirectory(testAppPath, tempDir);
        var tempAppJsonPath = Path.Combine(tempDir, "app.json");

        try
        {
            // Act
            var result = await _service.CompileAsync(tempAppJsonPath, compilerPath, null, suppressWarnings: true);

            // Assert
            Assert.True(result.Success, $"Compilation should succeed: {result.Message}");

            // Warnings should NOT be visible in raw output
            var combinedOutput = (result.StdOut ?? "") + (result.StdErr ?? "");
            Assert.DoesNotContain(": warning ", combinedOutput);

            // Warnings list should be empty
            Assert.Empty(result.Warnings);

            // Compilation message should still be there
            Assert.NotNull(result.AppPath);
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
    public async Task CompileAsync_AppWithErrors_SuppressWarningsTrue_StillShowsErrors()
    {
        // Arrange
        var compilerPath = GetCompilerPath();
        if (!File.Exists(compilerPath))
        {
            return; // Skip if compiler not available
        }

        var testAppPath = Path.Combine(_fixturesPath, "AppWithErrors");
        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-test-{Guid.NewGuid()}");
        CopyDirectory(testAppPath, tempDir);
        var tempAppJsonPath = Path.Combine(tempDir, "app.json");

        try
        {
            // Act
            var result = await _service.CompileAsync(tempAppJsonPath, compilerPath, null, suppressWarnings: true);

            // Assert
            Assert.False(result.Success, "Compilation should fail due to errors");

            // Errors should still be visible in raw output (not filtered)
            var combinedOutput = (result.StdOut ?? "") + (result.StdErr ?? "");
            Assert.Contains(": error ", combinedOutput);

            // Warnings should be filtered out
            Assert.DoesNotContain(": warning ", combinedOutput);

            // Errors should be in the parsed list
            Assert.NotEmpty(result.Errors);

            // Warnings list should be empty (suppressed)
            Assert.Empty(result.Warnings);
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
    public async Task CompileAsync_AppWithErrors_SuppressWarningsFalse_ShowsBothErrorsAndWarnings()
    {
        // Arrange
        var compilerPath = GetCompilerPath();
        if (!File.Exists(compilerPath))
        {
            return; // Skip if compiler not available
        }

        var testAppPath = Path.Combine(_fixturesPath, "AppWithErrors");
        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-test-{Guid.NewGuid()}");
        CopyDirectory(testAppPath, tempDir);
        var tempAppJsonPath = Path.Combine(tempDir, "app.json");

        try
        {
            // Act
            var result = await _service.CompileAsync(tempAppJsonPath, compilerPath, null, suppressWarnings: false);

            // Assert
            Assert.False(result.Success, "Compilation should fail due to errors");

            // Both errors and warnings should be visible
            var combinedOutput = (result.StdOut ?? "") + (result.StdErr ?? "");
            Assert.Contains(": error ", combinedOutput);
            Assert.Contains(": warning ", combinedOutput);

            // Both should be in parsed lists
            Assert.NotEmpty(result.Errors);
            Assert.NotEmpty(result.Warnings);
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
