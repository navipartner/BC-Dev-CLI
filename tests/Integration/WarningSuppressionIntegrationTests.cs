using BCDev.Services;
using Xunit;
using Xunit.Abstractions;

namespace BCDev.Tests.Integration;

/// <summary>
/// Integration tests for warning suppression functionality.
/// Tests that -suppressWarnings properly filters warnings from output while preserving errors.
/// </summary>
[Trait("Category", "Slow")]
public class WarningSuppressionIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly ArtifactService _artifactService;
    private readonly CompilerService _compilerService;

    public WarningSuppressionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _artifactService = new ArtifactService();
        _compilerService = new CompilerService();
    }

    [Fact]
    public async Task CompileAsync_AppWithWarnings_SuppressWarningsFalse_ShowsWarningsInOutput()
    {
        // Arrange
        var testAppPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "AppWithWarnings");
        var appJsonPath = Path.Combine(testAppPath, "app.json");

        Assert.True(File.Exists(appJsonPath), $"Test app.json should exist at {appJsonPath}");
        _output.WriteLine($"Test app: {testAppPath}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-warning-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            CopyDirectory(testAppPath, tempDir);
            var tempAppJsonPath = Path.Combine(tempDir, "app.json");

            // Get compiler
            var compilerPath = await GetCompilerPathAsync(tempAppJsonPath);

            // Act
            var result = await _compilerService.CompileAsync(tempAppJsonPath, compilerPath, null, suppressWarnings: false);

            // Assert
            _output.WriteLine($"Success: {result.Success}");
            _output.WriteLine($"StdOut: {result.StdOut}");
            _output.WriteLine($"StdErr: {result.StdErr}");
            _output.WriteLine($"Warnings: {result.Warnings.Count}");
            _output.WriteLine($"Errors: {result.Errors.Count}");

            Assert.True(result.Success, $"Compilation should succeed: {result.Message}");

            // Warnings should be visible in raw output
            var combinedOutput = (result.StdOut ?? "") + (result.StdErr ?? "");
            Assert.Contains(": warning ", combinedOutput);
            Assert.Contains("AL0432", combinedOutput);

            // Warnings should also be in the parsed list
            Assert.NotEmpty(result.Warnings);
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public async Task CompileAsync_AppWithWarnings_SuppressWarningsTrue_HidesWarningsFromOutput()
    {
        // Arrange
        var testAppPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "AppWithWarnings");
        var appJsonPath = Path.Combine(testAppPath, "app.json");

        Assert.True(File.Exists(appJsonPath), $"Test app.json should exist at {appJsonPath}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-warning-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            CopyDirectory(testAppPath, tempDir);
            var tempAppJsonPath = Path.Combine(tempDir, "app.json");

            var compilerPath = await GetCompilerPathAsync(tempAppJsonPath);

            // Act
            var result = await _compilerService.CompileAsync(tempAppJsonPath, compilerPath, null, suppressWarnings: true);

            // Assert
            _output.WriteLine($"Success: {result.Success}");
            _output.WriteLine($"StdOut: {result.StdOut}");
            _output.WriteLine($"StdErr: {result.StdErr}");
            _output.WriteLine($"Warnings: {result.Warnings.Count}");

            Assert.True(result.Success, $"Compilation should succeed: {result.Message}");

            // Warnings should NOT be visible in raw output
            var combinedOutput = (result.StdOut ?? "") + (result.StdErr ?? "");
            Assert.DoesNotContain(": warning ", combinedOutput);

            // Warnings list should be empty
            Assert.Empty(result.Warnings);

            // App should still be compiled
            Assert.NotNull(result.AppPath);
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public async Task CompileAsync_AppWithErrors_SuppressWarningsTrue_StillShowsErrors()
    {
        // Arrange
        var testAppPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "AppWithErrors");
        var appJsonPath = Path.Combine(testAppPath, "app.json");

        Assert.True(File.Exists(appJsonPath), $"Test app.json should exist at {appJsonPath}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-warning-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            CopyDirectory(testAppPath, tempDir);
            var tempAppJsonPath = Path.Combine(tempDir, "app.json");

            var compilerPath = await GetCompilerPathAsync(tempAppJsonPath);

            // Act
            var result = await _compilerService.CompileAsync(tempAppJsonPath, compilerPath, null, suppressWarnings: true);

            // Assert
            _output.WriteLine($"Success: {result.Success}");
            _output.WriteLine($"StdOut: {result.StdOut}");
            _output.WriteLine($"StdErr: {result.StdErr}");
            _output.WriteLine($"Errors: {result.Errors.Count}");
            _output.WriteLine($"Warnings: {result.Warnings.Count}");

            Assert.False(result.Success, "Compilation should fail due to errors");

            // Errors should still be visible in raw output
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
            CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public async Task CompileAsync_AppWithErrors_SuppressWarningsFalse_ShowsBothErrorsAndWarnings()
    {
        // Arrange
        var testAppPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "AppWithErrors");
        var appJsonPath = Path.Combine(testAppPath, "app.json");

        Assert.True(File.Exists(appJsonPath), $"Test app.json should exist at {appJsonPath}");

        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-warning-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            CopyDirectory(testAppPath, tempDir);
            var tempAppJsonPath = Path.Combine(tempDir, "app.json");

            var compilerPath = await GetCompilerPathAsync(tempAppJsonPath);

            // Act
            var result = await _compilerService.CompileAsync(tempAppJsonPath, compilerPath, null, suppressWarnings: false);

            // Assert
            _output.WriteLine($"Success: {result.Success}");
            _output.WriteLine($"StdOut: {result.StdOut}");
            _output.WriteLine($"StdErr: {result.StdErr}");
            _output.WriteLine($"Errors: {result.Errors.Count}");
            _output.WriteLine($"Warnings: {result.Warnings.Count}");

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
            CleanupTempDir(tempDir);
        }
    }

    private async Task<string> GetCompilerPathAsync(string appJsonPath)
    {
        _output.WriteLine("Resolving compiler version from app.json...");
        var version = await _artifactService.ResolveVersionFromAppJsonAsync(appJsonPath);
        _output.WriteLine($"BC version: {version}");

        _output.WriteLine("Downloading compiler artifacts...");
        await _artifactService.EnsureArtifactsAsync(version);

        var compilerPath = _artifactService.GetCachedCompilerPath(version);
        Assert.NotNull(compilerPath);
        Assert.True(File.Exists(compilerPath), $"Compiler should exist at {compilerPath}");
        _output.WriteLine($"Compiler: {compilerPath}");

        // Verify compiler can run
        Assert.True(CanRunCompiler(compilerPath),
            "Compiler must be able to run. BC compiler is x64 only - on ARM64 macOS requires Rosetta + x64 .NET runtime.");

        return compilerPath;
    }

    private bool CanRunCompiler(string compilerPath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = "",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            return output.Contains("AL Compiler") || output.Contains("Microsoft");
        }
        catch
        {
            return false;
        }
    }

    private void CleanupTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
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
            if (Path.GetFileName(dir).StartsWith(".")) continue;

            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}
