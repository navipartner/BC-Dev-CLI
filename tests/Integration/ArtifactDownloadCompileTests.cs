using System.Diagnostics;
using System.Runtime.InteropServices;
using BCDev.Services;
using Xunit;
using Xunit.Abstractions;

namespace BCDev.Tests.Integration;

/// <summary>
/// End-to-end test that downloads BC compiler via ArtifactService and compiles a test app.
/// This test is slow on first run (~268MB download) but fast after caching.
///
/// NOTE: BC compiler is x64 only. On ARM64 macOS, requires x64 .NET runtime (Rosetta).
/// </summary>
[Trait("Category", "Slow")]
public class ArtifactDownloadCompileTests
{
    private readonly ITestOutputHelper _output;
    private readonly ArtifactService _artifactService;
    private readonly CompilerService _compilerService;

    public ArtifactDownloadCompileTests(ITestOutputHelper output)
    {
        _output = output;
        _artifactService = new ArtifactService();
        _compilerService = new CompilerService();
    }

    /// <summary>
    /// Checks if the compiler can execute on this platform.
    /// BC compiler is x64 only - on ARM64 macOS needs Rosetta + x64 .NET runtime.
    /// </summary>
    private (bool canRun, string reason) CanRunCompiler(string compilerPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = "", // alc with no args shows version and error (but runs)
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (false, "Process.Start returned null");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            // alc outputs "Microsoft (R) AL Compiler" to stderr and returns error code 1 with no args
            // If we see "AL Compiler" in output, it ran successfully
            var output = stdout + stderr;
            if (output.Contains("AL Compiler") || output.Contains("Microsoft"))
            {
                return (true, "Compiler runs successfully");
            }

            return (false, $"Unexpected output: {output.Substring(0, Math.Min(200, output.Length))}");
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    [Fact]
    public async Task DownloadAndCompile_SimpleTestApp_Succeeds()
    {
        // Arrange - use SimpleTestApp which has NO BC dependencies
        var testAppPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "SimpleTestApp");
        var appJsonPath = Path.Combine(testAppPath, "app.json");

        // Verify test app exists
        Assert.True(File.Exists(appJsonPath), $"Test app.json should exist at {appJsonPath}");
        _output.WriteLine($"Test app: {testAppPath}");

        // Get version from app.json
        var version = await _artifactService.ResolveVersionFromAppJsonAsync(appJsonPath);
        _output.WriteLine($"Resolved version: {version}");

        // Act - Step 1: Download/cache artifacts
        _output.WriteLine($"Ensuring artifacts for BC {version}...");
        var cachePath = await _artifactService.EnsureArtifactsAsync(version);
        _output.WriteLine($"Cache path: {cachePath}");

        // Verify compiler was downloaded
        var compilerPath = _artifactService.GetCachedCompilerPath(version);
        Assert.NotNull(compilerPath);
        Assert.True(File.Exists(compilerPath), $"Compiler should exist at {compilerPath}");
        _output.WriteLine($"Compiler: {compilerPath}");

        // Verify compiler can actually run on this platform
        var (canRun, reason) = CanRunCompiler(compilerPath);
        _output.WriteLine($"Compiler check: canRun={canRun}, reason={reason}");
        Assert.True(canRun, $"Compiler must be runnable. Reason: {reason}");

        // Verify client DLL was downloaded
        var clientDllPath = _artifactService.GetCachedClientDllPath(version);
        Assert.NotNull(clientDllPath);
        Assert.True(File.Exists(clientDllPath), $"Client DLL should exist at {clientDllPath}");
        _output.WriteLine($"Client DLL: {clientDllPath}");

        // Act - Step 2: Compile the test app
        _output.WriteLine("Compiling test app...");

        // Create temp directory for output
        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-e2e-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Copy test app to temp to avoid polluting source
            CopyDirectory(testAppPath, tempDir);
            var tempAppJsonPath = Path.Combine(tempDir, "app.json");

            var result = await _compilerService.CompileAsync(tempAppJsonPath, compilerPath, null);

            // Assert
            _output.WriteLine($"Compilation success: {result.Success}");
            _output.WriteLine($"Message: {result.Message}");
            if (!string.IsNullOrEmpty(result.StdOut))
                _output.WriteLine($"StdOut: {result.StdOut}");
            if (!string.IsNullOrEmpty(result.StdErr))
                _output.WriteLine($"StdErr: {result.StdErr}");

            Assert.True(result.Success, $"Compilation should succeed. Errors: {string.Join(", ", result.Errors.Select(e => e.Message))}");
            Assert.NotNull(result.AppPath);
            Assert.True(File.Exists(result.AppPath), $"Compiled .app should exist at {result.AppPath}");
            _output.WriteLine($"Compiled app: {result.AppPath}");
        }
        finally
        {
            // Cleanup temp directory
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureArtifacts_CachesCorrectly()
    {
        // Arrange
        const string version = "27.0";

        // Act - First call (may download)
        _output.WriteLine("First call to EnsureArtifactsAsync...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cachePath1 = await _artifactService.EnsureArtifactsAsync(version);
        sw.Stop();
        var firstCallMs = sw.ElapsedMilliseconds;
        _output.WriteLine($"First call: {firstCallMs}ms, path: {cachePath1}");

        // Act - Second call (should be cached)
        _output.WriteLine("Second call to EnsureArtifactsAsync...");
        sw.Restart();
        var cachePath2 = await _artifactService.EnsureArtifactsAsync(version);
        sw.Stop();
        var secondCallMs = sw.ElapsedMilliseconds;
        _output.WriteLine($"Second call: {secondCallMs}ms, path: {cachePath2}");

        // Assert
        Assert.Equal(cachePath1, cachePath2);
        Assert.True(secondCallMs < 100, $"Cached call should be fast (<100ms), was {secondCallMs}ms");
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
            // Skip hidden directories like .vscode
            if (Path.GetFileName(dir).StartsWith("."))
                continue;

            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}
