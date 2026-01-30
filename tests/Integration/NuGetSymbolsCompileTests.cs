using BCDev.Services;
using Xunit;
using Xunit.Abstractions;

namespace BCDev.Tests.Integration;

/// <summary>
/// Integration test that downloads symbols from NuGet, then compiles.
/// Tests the full workflow: NuGet symbol download -> AL compilation.
/// </summary>
[Trait("Category", "Slow")]
public class NuGetSymbolsCompileTests
{
    private readonly ITestOutputHelper _output;
    private readonly ArtifactService _artifactService;
    private readonly CompilerService _compilerService;
    private readonly SymbolService _symbolService;

    public NuGetSymbolsCompileTests(ITestOutputHelper output)
    {
        _output = output;
        _artifactService = new ArtifactService();
        _compilerService = new CompilerService();
        _symbolService = new SymbolService();
    }

    [Fact]
    public async Task DownloadNuGetSymbols_ThenCompile_Succeeds()
    {
        // Arrange - use BaseAppDependentApp fixture
        var testAppPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "BaseAppDependentApp");
        var appJsonPath = Path.Combine(testAppPath, "app.json");

        Assert.True(File.Exists(appJsonPath), $"Test app.json should exist at {appJsonPath}");
        _output.WriteLine($"Test app: {testAppPath}");

        // Create temp directory for the test
        var tempDir = Path.Combine(Path.GetTempPath(), $"bcdev-nuget-e2e-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Copy test app to temp
            CopyDirectory(testAppPath, tempDir);
            var tempAppJsonPath = Path.Combine(tempDir, "app.json");
            var packageCachePath = Path.Combine(tempDir, ".alpackages");

            _output.WriteLine($"Temp dir: {tempDir}");
            _output.WriteLine($"Package cache: {packageCachePath}");

            // Step 1: Download symbols from NuGet
            _output.WriteLine("Step 1: Downloading symbols from NuGet...");
            var symbolResult = await _symbolService.DownloadFromNuGetAsync(
                tempAppJsonPath, packageCachePath, "w1");

            _output.WriteLine($"Symbol download success: {symbolResult.Success}");
            _output.WriteLine($"Downloaded: {string.Join(", ", symbolResult.DownloadedSymbols)}");
            if (symbolResult.Failures.Count > 0)
            {
                foreach (var failure in symbolResult.Failures)
                {
                    _output.WriteLine($"  FAILED: {failure.Symbol} - {failure.Error}");
                }
            }

            Assert.True(symbolResult.Success,
                $"Symbol download should succeed. Failures: {string.Join(", ", symbolResult.Failures.Select(f => $"{f.Symbol}: {f.Error}"))}");
            Assert.True(symbolResult.DownloadedSymbols.Count > 0, "Should download at least one symbol");

            // Verify .alpackages has .app files
            var appFiles = Directory.GetFiles(packageCachePath, "*.app");
            _output.WriteLine($"Downloaded {appFiles.Length} .app files:");
            foreach (var appFile in appFiles)
            {
                _output.WriteLine($"  - {Path.GetFileName(appFile)}");
            }
            Assert.True(appFiles.Length > 0, "Should have .app files in package cache");

            // Step 2: Download compiler artifacts
            _output.WriteLine("Step 2: Ensuring compiler artifacts...");
            var version = await _artifactService.ResolveVersionFromAppJsonAsync(tempAppJsonPath);
            _output.WriteLine($"Resolved BC version: {version}");

            await _artifactService.EnsureArtifactsAsync(version);
            var compilerPath = _artifactService.GetCachedCompilerPath(version);
            Assert.NotNull(compilerPath);
            Assert.True(File.Exists(compilerPath), $"Compiler should exist at {compilerPath}");
            _output.WriteLine($"Compiler: {compilerPath}");

            // Verify compiler can run on this platform (required - no skipping)
            var canRun = CanRunCompiler(compilerPath);
            _output.WriteLine($"Compiler runnable: {canRun}");
            Assert.True(canRun, "Compiler must be able to run on this platform. BC compiler is x64 only - on ARM64 macOS requires Rosetta + x64 .NET runtime.");

            // Step 3: Compile with downloaded symbols
            _output.WriteLine("Step 3: Compiling with downloaded symbols...");
            var compileResult = await _compilerService.CompileAsync(tempAppJsonPath, compilerPath, null);

            _output.WriteLine($"Compilation success: {compileResult.Success}");
            _output.WriteLine($"Message: {compileResult.Message}");
            if (!string.IsNullOrEmpty(compileResult.StdOut))
                _output.WriteLine($"StdOut: {compileResult.StdOut}");
            if (!string.IsNullOrEmpty(compileResult.StdErr))
                _output.WriteLine($"StdErr: {compileResult.StdErr}");

            Assert.True(compileResult.Success,
                $"Compilation should succeed. Errors: {string.Join(", ", compileResult.Errors.Select(e => e.Message))}");
            Assert.NotNull(compileResult.AppPath);
            Assert.True(File.Exists(compileResult.AppPath), $"Compiled .app should exist at {compileResult.AppPath}");

            _output.WriteLine($"SUCCESS: Compiled app at {compileResult.AppPath}");
        }
        finally
        {
            // Cleanup
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
    }

    [Fact]
    public void DownloadNuGetSymbols_VersionFuzzyMatching_Works()
    {
        // Test that version fuzzy matching finds a compatible version
        // when the exact version doesn't exist in NuGet
        var versions = new List<string>
        {
            "26.0.27117.35882",
            "26.0.27795.36626",
            "26.1.28603.38032"
        };

        // Request 26.0.0.0 (generic version from app.json)
        var matched = NuGetFeedService.FindMatchingVersion(versions, "26.0.0.0");

        _output.WriteLine($"Requested: 26.0.0.0");
        _output.WriteLine($"Available: {string.Join(", ", versions)}");
        _output.WriteLine($"Matched: {matched}");

        Assert.NotNull(matched);
        Assert.StartsWith("26.0.", matched); // Should match 26.0.x, not 26.1.x
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
