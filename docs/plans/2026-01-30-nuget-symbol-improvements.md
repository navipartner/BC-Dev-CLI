# NuGet Symbol Improvements Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix NuGet symbol download to be opt-in (not default), add version fuzzy matching, add integration test for symbol download + compile workflow, and inject version from git tags into release binaries.

**Architecture:** Four changes: (1) Flip `-fromServer` flag semantics so server mode is default and add new `-fromNuGet` opt-in flag, (2) Add "closest higher version" matching in NuGetFeedService, (3) Add integration test that downloads symbols via NuGet then compiles, (4) Update release pipeline to extract version from git tag and embed in compiled binaries.

**Tech Stack:** C# / .NET 10 / xunit / System.CommandLine

---

## Task 1: Flip Flag Semantics - Remove `-fromServer`, Make Server Default, Add `-fromNuGet`

**Files:**
- Modify: `src/Commands/SymbolsCommand.cs:29-32` (remove fromServer option)
- Modify: `src/Commands/SymbolsCommand.cs:59-119` (flip logic)

**Step 1: Write failing test for new flag behavior**

Create test file: `tests/Unit/SymbolsCommandFlagTests.cs`

```csharp
using System.CommandLine;
using System.CommandLine.Parsing;
using BCDev.Commands;
using Xunit;

namespace BCDev.Tests.Unit;

public class SymbolsCommandFlagTests
{
    [Fact]
    public void SymbolsCommand_HasFromNuGetOption()
    {
        var command = SymbolsCommand.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "fromNuGet");

        Assert.NotNull(option);
        Assert.False(((Option<bool>)option).Parse("-fromNuGet false").GetValueForOption((Option<bool>)option));
    }

    [Fact]
    public void SymbolsCommand_DoesNotHaveFromServerOption()
    {
        var command = SymbolsCommand.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "fromServer");

        Assert.Null(option);
    }

    [Fact]
    public void SymbolsCommand_FromNuGetDefaultsToFalse()
    {
        var command = SymbolsCommand.Create();
        var option = (Option<bool>)command.Options.First(o => o.Name == "fromNuGet");

        // Parse with no -fromNuGet flag
        var result = command.Parse("-appJsonPath test.json");
        var value = result.GetValueForOption(option);

        Assert.False(value);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj --filter "SymbolsCommandFlagTests" -v n`
Expected: FAIL - "fromNuGet" option doesn't exist

**Step 3: Update SymbolsCommand.cs - replace `-fromServer` with `-fromNuGet`**

In `src/Commands/SymbolsCommand.cs`, replace lines 29-32:

```csharp
var fromNuGetOption = new Option<bool>(
    name: "-fromNuGet",
    description: "Download from NuGet feeds instead of BC server (experimental)",
    getDefaultValue: () => false);
```

Update line 50 (was `command.AddOption(fromServerOption)`):
```csharp
command.AddOption(fromNuGetOption);
```

Update lines 59-68 to use new option name:
```csharp
command.SetHandler(async (context) =>
{
    var appJsonPath = context.ParseResult.GetValueForOption(appJsonPathOption)!;
    var packageCachePath = context.ParseResult.GetValueForOption(packageCachePathOption);
    var country = context.ParseResult.GetValueForOption(countryOption)!;
    var fromNuGet = context.ParseResult.GetValueForOption(fromNuGetOption);
    var launchJsonPath = context.ParseResult.GetValueForOption(launchJsonPathOption);
    var launchJsonName = context.ParseResult.GetValueForOption(launchJsonNameOption);
    var username = context.ParseResult.GetValueForOption(usernameOption);
    var password = context.ParseResult.GetValueForOption(passwordOption);

    await ExecuteAsync(appJsonPath, packageCachePath, country, fromNuGet,
        launchJsonPath, launchJsonName, username, password);
});
```

**Step 4: Update ExecuteAsync signature and flip the logic**

Change signature (line 77-85):
```csharp
private static async Task ExecuteAsync(
    string appJsonPath,
    string? packageCachePath,
    string country,
    bool fromNuGet,  // renamed from fromServer
    string? launchJsonPath,
    string? launchJsonName,
    string? username,
    string? password)
```

Flip the if/else logic (lines 91-115):
```csharp
var symbolService = new SymbolService();

Models.SymbolsResult result;

if (fromNuGet)
{
    // NuGet mode (opt-in)
    result = await symbolService.DownloadFromNuGetAsync(
        appJsonPath, packageCachePath, country);
}
else
{
    // Server mode (default) - validate required options
    if (string.IsNullOrEmpty(launchJsonPath))
    {
        Console.Error.WriteLine("Error: -launchJsonPath is required (use -fromNuGet to download from NuGet feeds instead)");
        Environment.ExitCode = 1;
        return;
    }
    if (string.IsNullOrEmpty(launchJsonName))
    {
        Console.Error.WriteLine("Error: -launchJsonName is required (use -fromNuGet to download from NuGet feeds instead)");
        Environment.ExitCode = 1;
        return;
    }

    result = await symbolService.DownloadFromServerAsync(
        appJsonPath, launchJsonPath, launchJsonName, packageCachePath, username, password);
}
```

**Step 5: Run test to verify it passes**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj --filter "SymbolsCommandFlagTests" -v n`
Expected: PASS

**Step 6: Run all existing tests to check for regressions**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj -v n`
Expected: All tests PASS

**Step 7: Commit**

```bash
git add tests/Unit/SymbolsCommandFlagTests.cs src/Commands/SymbolsCommand.cs
git commit -m "$(cat <<'EOF'
refactor(symbols): make server mode default, add -fromNuGet opt-in flag

BREAKING: Removes -fromServer flag. Server mode is now default behavior.
Use -fromNuGet flag to opt into experimental NuGet feed downloads.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Add "Closest Higher Version" Matching for NuGet Packages

**Files:**
- Modify: `src/Services/NuGetFeedService.cs:276-281` (FindMatchingVersion method)
- Create: `tests/Unit/NuGetVersionMatchingTests.cs`

**Step 1: Write failing tests for version matching**

Create: `tests/Unit/NuGetVersionMatchingTests.cs`

```csharp
using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class NuGetVersionMatchingTests
{
    [Fact]
    public void FindMatchingVersion_ExactMatch_ReturnsExact()
    {
        var versions = new List<string> { "27.0.45024", "27.0.45025", "27.1.46000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.45024");
        Assert.Equal("27.0.45024", result);
    }

    [Fact]
    public void FindMatchingVersion_NoExact_ReturnsClosestHigher()
    {
        var versions = new List<string> { "27.0.45024", "27.0.45100", "27.1.46000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.45050");
        Assert.Equal("27.0.45100", result);
    }

    [Fact]
    public void FindMatchingVersion_RequestedTooHigh_ReturnsNull()
    {
        var versions = new List<string> { "27.0.45024", "27.0.45100" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "28.0.0");
        Assert.Null(result);
    }

    [Fact]
    public void FindMatchingVersion_FourPartVersion_MatchesMajorMinor()
    {
        // app.json says 27.0.0.0 but NuGet has 27.0.45024
        var versions = new List<string> { "26.5.44000", "27.0.45024", "27.0.45100", "27.1.46000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.0.0");
        Assert.Equal("27.0.45024", result); // Closest higher in 27.0.x
    }

    [Fact]
    public void FindMatchingVersion_ThreePartToFourPart_Works()
    {
        // Platform uses 3-part, looking for 4-part equivalent
        var versions = new List<string> { "27.0.45024", "27.0.45100", "27.1.46000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.0.0");
        Assert.Equal("27.0.45024", result);
    }

    [Fact]
    public void FindMatchingVersion_PrefersSameMajorMinor()
    {
        var versions = new List<string> { "26.5.44000", "27.0.45024", "27.1.46000", "28.0.50000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.38460");
        Assert.Equal("27.0.45024", result); // Stay in 27.0.x, not jump to 27.1
    }

    [Fact]
    public void FindMatchingVersion_EmptyVersions_ReturnsNull()
    {
        var versions = new List<string>();
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.0.0");
        Assert.Null(result);
    }

    [Fact]
    public void FindMatchingVersion_CaseInsensitive()
    {
        var versions = new List<string> { "27.0.45024" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.45024");
        Assert.Equal("27.0.45024", result);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj --filter "NuGetVersionMatchingTests" -v n`
Expected: FAIL - tests expecting fuzzy matching will fail

**Step 3: Implement version matching logic**

Replace `FindMatchingVersion` method in `src/Services/NuGetFeedService.cs` (lines 273-281):

```csharp
/// <summary>
/// Find matching version from available versions.
/// Priority: exact match > closest higher version in same major.minor
/// </summary>
public static string? FindMatchingVersion(List<string> availableVersions, string targetVersion)
{
    if (availableVersions.Count == 0) return null;

    // Try exact match first
    var exact = availableVersions.FirstOrDefault(v =>
        v.Equals(targetVersion, StringComparison.OrdinalIgnoreCase));
    if (exact != null) return exact;

    // Parse target version
    var targetParts = ParseVersionParts(targetVersion);
    if (targetParts == null) return null;

    // Find closest higher version in same major.minor
    string? bestMatch = null;
    (int major, int minor, int build, int rev)? bestParts = null;

    foreach (var version in availableVersions)
    {
        var parts = ParseVersionParts(version);
        if (parts == null) continue;

        // Must match major.minor
        if (parts.Value.major != targetParts.Value.major ||
            parts.Value.minor != targetParts.Value.minor)
            continue;

        // Must be >= target build
        if (parts.Value.build < targetParts.Value.build)
            continue;

        // If same build, revision must be >= target
        if (parts.Value.build == targetParts.Value.build &&
            parts.Value.rev < targetParts.Value.rev)
            continue;

        // Is this better (lower) than current best?
        if (bestParts == null ||
            parts.Value.build < bestParts.Value.build ||
            (parts.Value.build == bestParts.Value.build && parts.Value.rev < bestParts.Value.rev))
        {
            bestMatch = version;
            bestParts = parts;
        }
    }

    return bestMatch;
}

/// <summary>
/// Parse version string into parts. Handles 3-part (27.0.45024) and 4-part (27.0.45024.0) formats.
/// </summary>
private static (int major, int minor, int build, int rev)? ParseVersionParts(string version)
{
    var segments = version.Split('.');
    if (segments.Length < 3) return null;

    if (!int.TryParse(segments[0], out var major)) return null;
    if (!int.TryParse(segments[1], out var minor)) return null;
    if (!int.TryParse(segments[2], out var build)) return null;

    var rev = 0;
    if (segments.Length >= 4)
    {
        int.TryParse(segments[3], out rev);
    }

    return (major, minor, build, rev);
}
```

**Step 4: Run tests to verify they pass**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj --filter "NuGetVersionMatchingTests" -v n`
Expected: PASS

**Step 5: Update error message for unmatched versions**

In `src/Services/NuGetFeedService.cs`, update the error in `DownloadSymbolAsync` around line 142:

```csharp
if (matchedVersion == null)
{
    throw new InvalidOperationException(
        $"No compatible version found for {packageId} (requested {symbol.Version}). " +
        $"Available versions in {targetMajor}.{targetMinor}.x: {FormatAvailableVersions(FilterByMajorMinor(versions, symbol.Version))}. " +
        $"All available: {FormatAvailableVersions(versions)}");
}
```

Add helper method after `FormatAvailableVersions`:

```csharp
/// <summary>
/// Filter versions to only those matching the major.minor of target
/// </summary>
private static List<string> FilterByMajorMinor(List<string> versions, string targetVersion)
{
    var targetParts = targetVersion.Split('.');
    if (targetParts.Length < 2) return versions;
    var prefix = $"{targetParts[0]}.{targetParts[1]}.";
    return versions.Where(v => v.StartsWith(prefix)).ToList();
}
```

**Step 6: Run all tests**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj -v n`
Expected: PASS

**Step 7: Commit**

```bash
git add src/Services/NuGetFeedService.cs tests/Unit/NuGetVersionMatchingTests.cs
git commit -m "$(cat <<'EOF'
feat(nuget): add closest-higher-version matching for NuGet symbols

When exact version not found in NuGet feed, finds the closest higher
version within the same major.minor. Handles both 3-part (27.0.45024)
and 4-part (27.0.0.0) version formats.

Addresses feedback that users had to guess exact NuGet versions.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Add Integration Test for NuGet Symbol Download + Compile

**Files:**
- Create: `tests/Fixtures/BaseAppDependentApp/app.json`
- Create: `tests/Fixtures/BaseAppDependentApp/src/HelloWorld.Codeunit.al`
- Create: `tests/Integration/NuGetSymbolsCompileTests.cs`

**Step 1: Create test fixture app that depends on Base Application**

Create directory: `tests/Fixtures/BaseAppDependentApp/`

Create: `tests/Fixtures/BaseAppDependentApp/app.json`

```json
{
  "id": "55555555-5555-5555-5555-555555555555",
  "name": "BaseApp Dependent Test",
  "publisher": "TestPublisher",
  "version": "1.0.0.0",
  "brief": "Test app that depends on Base Application",
  "description": "For testing NuGet symbol download followed by compilation",
  "platform": "26.0.0.0",
  "application": "26.0.0.0",
  "runtime": "13.0",
  "target": "OnPrem",
  "idRanges": [
    {
      "from": 50200,
      "to": 50299
    }
  ],
  "dependencies": [
    {
      "id": "437dbf0e-84ff-417a-965d-ed2bb9650972",
      "name": "Base Application",
      "publisher": "Microsoft",
      "version": "26.0.0.0"
    }
  ],
  "features": [
    "NoImplicitWith"
  ]
}
```

Note: Using BC 26.0 as it's more likely to have complete NuGet coverage than 27.x (which has Business Foundation issues per feedback).

Create: `tests/Fixtures/BaseAppDependentApp/src/HelloWorld.Codeunit.al`

```al
codeunit 50200 "Hello World"
{
    procedure SayHello(): Text
    begin
        exit('Hello from BaseApp dependent test');
    end;
}
```

**Step 2: Write the integration test**

Create: `tests/Integration/NuGetSymbolsCompileTests.cs`

```csharp
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
    private readonly NuGetFeedService _nugetService;
    private readonly ArtifactService _artifactService;
    private readonly CompilerService _compilerService;
    private readonly SymbolService _symbolService;

    public NuGetSymbolsCompileTests(ITestOutputHelper output)
    {
        _output = output;
        _nugetService = new NuGetFeedService();
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

            // Check if compiler can run on this platform
            if (!CanRunCompiler(compilerPath))
            {
                _output.WriteLine("SKIP: Compiler cannot run on this platform (likely ARM64 without Rosetta)");
                return;
            }

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
            _nugetService.Dispose();
        }
    }

    [Fact]
    public async Task DownloadNuGetSymbols_VersionFuzzyMatching_Works()
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
```

**Step 3: Run tests (may fail if NuGet doesn't have the versions)**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj --filter "NuGetSymbolsCompileTests" -v n`

If tests fail due to missing NuGet packages, adjust the app.json version numbers based on error output showing available versions.

**Step 4: Verify all tests pass**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add tests/Fixtures/BaseAppDependentApp/ tests/Integration/NuGetSymbolsCompileTests.cs
git commit -m "$(cat <<'EOF'
test(integration): add NuGet symbol download + compile e2e test

Adds integration test that:
1. Downloads symbols from NuGet feeds (Base Application)
2. Downloads BC compiler
3. Compiles test app with downloaded symbols

Uses BC 26.0 which has more complete NuGet coverage than 27.x.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Inject Version from Git Tag into Release Binaries

**Files:**
- Modify: `src/bcdev.csproj` (add version properties)
- Modify: `.github/workflows/release.yml` (extract tag version, pass to build)
- Modify: `src/Program.cs` (enable --version flag)

**Step 1: Update bcdev.csproj with version properties**

In `src/bcdev.csproj`, add version properties inside the `<PropertyGroup>`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>BCDev</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>bcdev</AssemblyName>

    <!-- Version defaults for local builds, overridden by CI -->
    <Version>0.0.0-local</Version>
    <InformationalVersion>0.0.0-local</InformationalVersion>
    <FileVersion>0.0.0.0</FileVersion>
    <AssemblyVersion>0.0.0.0</AssemblyVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    <PackageReference Include="System.ServiceModel.Primitives" Version="8.0.0" />
    <PackageReference Include="Microsoft.Identity.Client" Version="4.61.3" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <!-- Override vulnerable transitive dependency from System.ServiceModel.Primitives -->
    <PackageReference Include="System.Security.Cryptography.Pkcs" Version="9.0.1" />
  </ItemGroup>

</Project>
```

**Step 2: Build locally to verify it compiles**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet build src/bcdev.csproj`
Expected: Build succeeds

**Step 3: Update release.yml to extract version from tag and pass to build**

Replace the "Publish single-file" step in `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        include:
          - os: windows-latest
            rid: win-x64
            artifact: bcdev-win-x64.exe
          - os: ubuntu-latest
            rid: linux-x64
            artifact: bcdev-linux-x64
          - os: ubuntu-latest
            rid: linux-arm64
            artifact: bcdev-linux-arm64
          - os: macos-latest
            rid: osx-x64
            artifact: bcdev-osx-x64
          - os: macos-latest
            rid: osx-arm64
            artifact: bcdev-osx-arm64

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Extract version from tag
        id: version
        shell: bash
        run: |
          # Extract tag name (e.g., v0.5 -> 0.5)
          TAG=${GITHUB_REF#refs/tags/v}
          echo "tag=$TAG" >> $GITHUB_OUTPUT

          # Convert to valid .NET version (e.g., 0.5 -> 0.5.0, 0.5.1 -> 0.5.1)
          if [[ "$TAG" =~ ^[0-9]+\.[0-9]+$ ]]; then
            VERSION="${TAG}.0"
          else
            VERSION="$TAG"
          fi
          echo "version=$VERSION" >> $GITHUB_OUTPUT

          # File version needs 4 parts
          if [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
            FILE_VERSION="${VERSION}.0"
          else
            FILE_VERSION="$VERSION"
          fi
          echo "file_version=$FILE_VERSION" >> $GITHUB_OUTPUT

          echo "Tag: $TAG, Version: $VERSION, FileVersion: $FILE_VERSION"

      - name: Restore dependencies
        run: dotnet restore src/bcdev.csproj

      - name: Publish single-file
        shell: bash
        run: |
          dotnet publish src/bcdev.csproj \
            -c Release \
            -r ${{ matrix.rid }} \
            -p:PublishSingleFile=true \
            -p:SelfContained=true \
            -p:IncludeNativeLibrariesForSelfExtract=true \
            -p:Version=${{ steps.version.outputs.version }} \
            -p:InformationalVersion=${{ steps.version.outputs.tag }} \
            -p:FileVersion=${{ steps.version.outputs.file_version }} \
            -p:AssemblyVersion=${{ steps.version.outputs.file_version }} \
            -o publish

      - name: Prepare artifact
        shell: bash
        run: |
          if [ "${{ matrix.rid }}" == "win-x64" ]; then
            cp publish/bcdev.exe ${{ matrix.artifact }}
          else
            cp publish/bcdev ${{ matrix.artifact }}
          fi

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: ${{ matrix.artifact }}
          path: ${{ matrix.artifact }}

  release:
    needs: build
    runs-on: ubuntu-latest
    permissions:
      contents: write

    steps:
      - uses: actions/checkout@v4

      - name: Download all artifacts
        uses: actions/download-artifact@v4
        with:
          path: artifacts

      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          generate_release_notes: true
          files: |
            artifacts/bcdev-win-x64.exe/bcdev-win-x64.exe
            artifacts/bcdev-linux-x64/bcdev-linux-x64
            artifacts/bcdev-linux-arm64/bcdev-linux-arm64
            artifacts/bcdev-osx-x64/bcdev-osx-x64
            artifacts/bcdev-osx-arm64/bcdev-osx-arm64
```

**Step 4: Enable --version flag in Program.cs**

System.CommandLine's RootCommand automatically supports `--version` when assembly has `InformationalVersion`. Update `src/Program.cs` to explicitly enable it:

```csharp
using System.CommandLine;
using System.Reflection;
using BCDev.Commands;

namespace BCDev;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("BC Dev CLI - Cross-platform tool for Business Central development operations");

        // Add compile command
        rootCommand.AddCommand(CompileCommand.Create());

        // Add publish command
        rootCommand.AddCommand(PublishCommand.Create());

        // Add test command
        rootCommand.AddCommand(TestCommand.Create());

        // Add symbols command
        rootCommand.AddCommand(SymbolsCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Gets the assembly version for display (used by System.CommandLine --version)
    /// </summary>
    public static string GetVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
    }
}
```

Note: System.CommandLine automatically handles `--version` by reading `AssemblyInformationalVersionAttribute`. No explicit code needed beyond setting the assembly attribute via csproj.

**Step 5: Test locally that --version works**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet run --project src/bcdev.csproj -- --version`
Expected: `0.0.0-local` (the default version)

**Step 6: Run all tests**

Run: `/opt/homebrew/opt/dotnet@8/bin/dotnet test tests/bcdev.Tests.csproj -v n`
Expected: PASS

**Step 7: Commit**

```bash
git add src/bcdev.csproj .github/workflows/release.yml src/Program.cs
git commit -m "$(cat <<'EOF'
feat(release): inject version from git tag into binaries

- Add version properties to bcdev.csproj with local defaults
- Extract version from git tag in release workflow (v0.5 -> 0.5.0)
- Pass version to dotnet publish as MSBuild properties
- --version flag now shows actual release version

Binaries now have correct version embedded in metadata.

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
EOF
)"
```

---

## Summary

| Task | Description | Estimated Steps |
|------|-------------|-----------------|
| 1 | Flip flag semantics: remove `-fromServer`, add `-fromNuGet` | 7 steps |
| 2 | Add closest-higher-version matching | 7 steps |
| 3 | Add NuGet + compile integration test | 5 steps |
| 4 | Inject version from git tag into release binaries | 7 steps |

**Total: 26 steps**

**Dependencies:**
- Task 2 should be done before Task 3 (version matching needed for integration test to work with generic versions)
- Task 1 is independent
- Task 4 is independent

**Testing Notes:**
- Integration tests are marked `[Trait("Category", "Slow")]` and download from real NuGet feeds
- If NuGet doesn't have expected versions, adjust test fixture app.json based on error messages
- Compiler tests require x64 runtime (Rosetta on ARM Mac)
- Version injection can only be fully tested by creating a git tag and running the release pipeline
