# Compiler Parameters & Remove Recompile Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add VS Code-style compiler options to the compile command and remove the -recompile flag from publish.

**Architecture:** Extend CompilerService.CompileAsync with nullable parameters that conditionally add alc.exe flags. Simplify PublishService by removing compile-related logic. Update CompileCommand and PublishCommand option definitions.

**Tech Stack:** .NET 8, System.CommandLine, xUnit

---

## Task 1: Add CompilerService Argument Building Tests

**Files:**
- Modify: `tests/Unit/CompilerServiceTests.cs`

**Step 1: Write test for argument building helper**

We need to expose argument building logic for testing. First, add tests that will drive the implementation.

```csharp
[Theory]
[InlineData(true, "/generatereportlayout+")]
[InlineData(false, "/generatereportlayout-")]
public void BuildArguments_GenerateReportLayout_AddsFlag(bool value, string expected)
{
    var args = CompilerService.BuildCompilerArguments(
        projectPath: "/project",
        outputPath: "/out.app",
        packageCachePath: null,
        generateReportLayout: value,
        parallel: null,
        maxDegreeOfParallelism: null,
        continueBuildOnError: null);

    Assert.Contains(expected, args);
}

[Theory]
[InlineData(true, "/parallel+")]
[InlineData(false, "/parallel-")]
public void BuildArguments_Parallel_AddsFlag(bool value, string expected)
{
    var args = CompilerService.BuildCompilerArguments(
        projectPath: "/project",
        outputPath: "/out.app",
        packageCachePath: null,
        generateReportLayout: null,
        parallel: value,
        maxDegreeOfParallelism: null,
        continueBuildOnError: null);

    Assert.Contains(expected, args);
}

[Theory]
[InlineData(1, "/maxdegreeofparallelism:1")]
[InlineData(4, "/maxdegreeofparallelism:4")]
[InlineData(8, "/maxdegreeofparallelism:8")]
public void BuildArguments_MaxDegreeOfParallelism_AddsFlag(int value, string expected)
{
    var args = CompilerService.BuildCompilerArguments(
        projectPath: "/project",
        outputPath: "/out.app",
        packageCachePath: null,
        generateReportLayout: null,
        parallel: null,
        maxDegreeOfParallelism: value,
        continueBuildOnError: null);

    Assert.Contains(expected, args);
}

[Theory]
[InlineData(true, "/continuebuildonerror+")]
[InlineData(false, "/continuebuildonerror-")]
public void BuildArguments_ContinueBuildOnError_AddsFlag(bool value, string expected)
{
    var args = CompilerService.BuildCompilerArguments(
        projectPath: "/project",
        outputPath: "/out.app",
        packageCachePath: null,
        generateReportLayout: null,
        parallel: null,
        maxDegreeOfParallelism: null,
        continueBuildOnError: value);

    Assert.Contains(expected, args);
}

[Fact]
public void BuildArguments_NullOptions_OmitsFlags()
{
    var args = CompilerService.BuildCompilerArguments(
        projectPath: "/project",
        outputPath: "/out.app",
        packageCachePath: null,
        generateReportLayout: null,
        parallel: null,
        maxDegreeOfParallelism: null,
        continueBuildOnError: null);

    Assert.DoesNotContain("/generatereportlayout", args);
    Assert.DoesNotContain("/parallel", args);
    Assert.DoesNotContain("/maxdegreeofparallelism", args);
    Assert.DoesNotContain("/continuebuildonerror", args);
}

[Fact]
public void BuildArguments_AllOptions_AddsAllFlags()
{
    var args = CompilerService.BuildCompilerArguments(
        projectPath: "/project",
        outputPath: "/out.app",
        packageCachePath: "/packages",
        generateReportLayout: true,
        parallel: true,
        maxDegreeOfParallelism: 4,
        continueBuildOnError: false);

    Assert.Contains("/project:", args);
    Assert.Contains("/out:", args);
    Assert.Contains("/packagecachepath:", args);
    Assert.Contains("/generatereportlayout+", args);
    Assert.Contains("/parallel+", args);
    Assert.Contains("/maxdegreeofparallelism:4", args);
    Assert.Contains("/continuebuildonerror-", args);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Unit/CompilerServiceTests.cs --filter "BuildArguments" -v n`
Expected: FAIL - `BuildCompilerArguments` method doesn't exist

**Step 3: Commit test file**

```bash
git add tests/Unit/CompilerServiceTests.cs
git commit -m "test: add compiler argument building tests"
```

---

## Task 2: Implement CompilerService Argument Building

**Files:**
- Modify: `src/Services/CompilerService.cs`

**Step 1: Extract and extend BuildCompilerArguments method**

Add this static method to `CompilerService` class:

```csharp
/// <summary>
/// Build compiler command-line arguments string
/// </summary>
public static string BuildCompilerArguments(
    string projectPath,
    string outputPath,
    string? packageCachePath,
    bool? generateReportLayout,
    bool? parallel,
    int? maxDegreeOfParallelism,
    bool? continueBuildOnError)
{
    var args = new List<string>
    {
        $"/project:\"{projectPath}\"",
        $"/out:\"{outputPath}\""
    };

    if (!string.IsNullOrEmpty(packageCachePath) && Directory.Exists(packageCachePath))
    {
        args.Add($"/packagecachepath:\"{packageCachePath}\"");
    }

    if (generateReportLayout.HasValue)
    {
        args.Add($"/generatereportlayout{(generateReportLayout.Value ? "+" : "-")}");
    }

    if (parallel.HasValue)
    {
        args.Add($"/parallel{(parallel.Value ? "+" : "-")}");
    }

    if (maxDegreeOfParallelism.HasValue)
    {
        args.Add($"/maxdegreeofparallelism:{maxDegreeOfParallelism.Value}");
    }

    if (continueBuildOnError.HasValue)
    {
        args.Add($"/continuebuildonerror{(continueBuildOnError.Value ? "+" : "-")}");
    }

    return string.Join(" ", args);
}
```

**Step 2: Run tests to verify they pass**

Run: `dotnet test tests/Unit/CompilerServiceTests.cs --filter "BuildArguments" -v n`
Expected: PASS

**Step 3: Commit**

```bash
git add src/Services/CompilerService.cs
git commit -m "feat: add BuildCompilerArguments with new compiler options"
```

---

## Task 3: Update CompileAsync Signature and Use BuildCompilerArguments

**Files:**
- Modify: `src/Services/CompilerService.cs`

**Step 1: Update CompileAsync method signature**

Change the `CompileAsync` method signature to:

```csharp
public async Task<CompileResult> CompileAsync(
    string appJsonPath,
    string compilerPath,
    string? packageCachePath,
    bool suppressWarnings = false,
    bool? generateReportLayout = null,
    bool? parallel = null,
    int? maxDegreeOfParallelism = null,
    bool? continueBuildOnError = null)
```

**Step 2: Replace inline argument building with BuildCompilerArguments call**

Replace the existing argument building block (around lines 86-96) with:

```csharp
// Build compiler arguments
var arguments = BuildCompilerArguments(
    appFolder,
    outputPath,
    packageCachePath,
    generateReportLayout,
    parallel,
    maxDegreeOfParallelism,
    continueBuildOnError);

// Execute compiler
var startInfo = new ProcessStartInfo
{
    FileName = compilerPath,
    Arguments = arguments,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};
```

**Step 3: Run all unit tests**

Run: `dotnet test tests/Unit -v n`
Expected: PASS (existing tests still work with default parameter values)

**Step 4: Commit**

```bash
git add src/Services/CompilerService.cs
git commit -m "refactor: use BuildCompilerArguments in CompileAsync"
```

---

## Task 4: Add CompileCommand Options

**Files:**
- Modify: `src/Commands/CompileCommand.cs`

**Step 1: Add new option definitions**

After the existing `suppressWarningsOption` (around line 26), add:

```csharp
var generateReportLayoutOption = new Option<bool?>(
    name: "-generateReportLayout",
    description: "Generate report layout files during compilation");

var parallelOption = new Option<bool?>(
    name: "-parallel",
    description: "Enable parallel compilation");

var maxDegreeOfParallelismOption = new Option<int?>(
    name: "-maxDegreeOfParallelism",
    description: "Maximum number of concurrent compilation tasks");

var continueBuildOnErrorOption = new Option<bool?>(
    name: "-continueBuildOnError",
    description: "Continue building even if errors are found");
```

**Step 2: Register options with command**

After `command.AddOption(suppressWarningsOption);` add:

```csharp
command.AddOption(generateReportLayoutOption);
command.AddOption(parallelOption);
command.AddOption(maxDegreeOfParallelismOption);
command.AddOption(continueBuildOnErrorOption);
```

**Step 3: Update handler to pass new options**

Replace the `SetHandler` call with:

```csharp
command.SetHandler(async (context) =>
{
    var appJsonPath = context.ParseResult.GetValueForOption(appJsonPathOption)!;
    var packageCachePath = context.ParseResult.GetValueForOption(packageCachePathOption);
    var suppressWarnings = context.ParseResult.GetValueForOption(suppressWarningsOption);
    var generateReportLayout = context.ParseResult.GetValueForOption(generateReportLayoutOption);
    var parallel = context.ParseResult.GetValueForOption(parallelOption);
    var maxDegreeOfParallelism = context.ParseResult.GetValueForOption(maxDegreeOfParallelismOption);
    var continueBuildOnError = context.ParseResult.GetValueForOption(continueBuildOnErrorOption);

    // Validate maxDegreeOfParallelism if provided
    if (maxDegreeOfParallelism.HasValue && maxDegreeOfParallelism.Value <= 0)
    {
        Console.Error.WriteLine("Error: -maxDegreeOfParallelism must be greater than 0");
        context.ExitCode = 1;
        return;
    }

    await ExecuteAsync(appJsonPath, packageCachePath, suppressWarnings,
        generateReportLayout, parallel, maxDegreeOfParallelism, continueBuildOnError);
});
```

**Step 4: Update ExecuteAsync signature and call**

```csharp
private static async Task ExecuteAsync(
    string appJsonPath,
    string? packageCachePath,
    bool suppressWarnings,
    bool? generateReportLayout,
    bool? parallel,
    int? maxDegreeOfParallelism,
    bool? continueBuildOnError)
{
    // Auto-download compiler based on app.json platform version
    var artifactService = new ArtifactService();
    var version = await artifactService.ResolveVersionFromAppJsonAsync(appJsonPath);
    await artifactService.EnsureArtifactsAsync(version);
    var compilerPath = artifactService.GetCachedCompilerPath(version)
        ?? throw new InvalidOperationException($"Failed to get compiler path for version {version}");

    var compilerService = new CompilerService();
    var result = await compilerService.CompileAsync(
        appJsonPath, compilerPath, packageCachePath, suppressWarnings,
        generateReportLayout, parallel, maxDegreeOfParallelism, continueBuildOnError);

    // Output result as JSON
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, JsonContext.Default.CompileResult));

    Environment.ExitCode = result.Success ? 0 : 1;
}
```

**Step 5: Build to verify no compile errors**

Run: `dotnet build`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add src/Commands/CompileCommand.cs
git commit -m "feat: add compiler options to compile command"
```

---

## Task 5: Simplify PublishService

**Files:**
- Modify: `src/Services/PublishService.cs`

**Step 1: Simplify PublishAsync signature**

Change the method signature from:

```csharp
public async Task<PublishResult> PublishAsync(
    bool recompile,
    string? appPath,
    string? appJsonPath,
    string? packageCachePath,
    string launchJsonPath,
    string launchJsonName,
    string? username,
    string? password)
```

To:

```csharp
public async Task<PublishResult> PublishAsync(
    string appPath,
    string launchJsonPath,
    string launchJsonName,
    string? username,
    string? password)
```

**Step 2: Remove recompile block**

Delete the entire recompile block (lines 43-65):

```csharp
// DELETE THIS BLOCK:
// If recompile flag is set, compile first (suppress warnings for cleaner output)
if (recompile)
{
    // Auto-download compiler based on app.json platform version
    var artifactService = new ArtifactService();
    var version = await artifactService.ResolveVersionFromAppJsonAsync(appJsonPath!);
    await artifactService.EnsureArtifactsAsync(version);
    var compilerPath = artifactService.GetCachedCompilerPath(version)
        ?? throw new InvalidOperationException($"Failed to get compiler path for version {version}");

    var compilerService = new CompilerService();
    var compileResult = await compilerService.CompileAsync(appJsonPath!, compilerPath, packageCachePath, suppressWarnings: true);

    if (!compileResult.Success)
    {
        result.Success = false;
        result.Message = "Compilation failed";
        result.Error = compileResult.Message;
        return result;
    }

    appPath = compileResult.AppPath;
}
```

**Step 3: Update null check since appPath is now required**

Change:

```csharp
if (string.IsNullOrEmpty(appPath) || !File.Exists(appPath))
```

To:

```csharp
if (!File.Exists(appPath))
```

**Step 4: Build to verify compile errors in PublishCommand (expected)**

Run: `dotnet build`
Expected: Build FAILS - PublishCommand.cs has wrong number of arguments

**Step 5: Commit PublishService changes**

```bash
git add src/Services/PublishService.cs
git commit -m "refactor: remove recompile logic from PublishService"
```

---

## Task 6: Update PublishCommand

**Files:**
- Modify: `src/Commands/PublishCommand.cs`

**Step 1: Remove recompile-related options**

Delete these option definitions:

```csharp
// DELETE: recompileOption (lines 13-16)
var recompileOption = new Option<bool>(
    name: "-recompile",
    description: "Compile the app before publishing",
    getDefaultValue: () => false);

// DELETE: appJsonPathOption (lines 23-25)
var appJsonPathOption = new Option<string?>(
    name: "-appJsonPath",
    description: "Path to app.json file (required with -recompile)");

// DELETE: packageCachePathOption (lines 27-29)
var packageCachePathOption = new Option<string?>(
    name: "-packageCachePath",
    description: "Path to .alpackages folder containing symbol packages (used with -recompile)");
```

**Step 2: Make appPath required**

Change:

```csharp
var appPathOption = new Option<string?>(
    name: "-appPath",
    description: "Path to .app file (required if not using -recompile)");
```

To:

```csharp
var appPathOption = new Option<string>(
    name: "-appPath",
    description: "Path to .app file")
{
    IsRequired = true
};
```

**Step 3: Remove deleted options from command registration**

Delete these lines:

```csharp
command.AddOption(recompileOption);
// Keep appPathOption
command.AddOption(appJsonPathOption);
command.AddOption(packageCachePathOption);
```

**Step 4: Simplify handler**

Replace the SetHandler with:

```csharp
command.SetHandler(async (context) =>
{
    var appPath = context.ParseResult.GetValueForOption(appPathOption)!;
    var launchJsonPath = context.ParseResult.GetValueForOption(launchJsonPathOption)!;
    var launchJsonName = context.ParseResult.GetValueForOption(launchJsonNameOption)!;
    var username = context.ParseResult.GetValueForOption(usernameOption);
    var password = context.ParseResult.GetValueForOption(passwordOption);

    await ExecuteAsync(appPath, launchJsonPath, launchJsonName, username, password);
});
```

**Step 5: Simplify ExecuteAsync**

Replace the entire `ExecuteAsync` method with:

```csharp
private static async Task ExecuteAsync(
    string appPath,
    string launchJsonPath,
    string launchJsonName,
    string? username,
    string? password)
{
    var publishService = new PublishService();
    var result = await publishService.PublishAsync(
        appPath, launchJsonPath, launchJsonName, username, password);

    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, JsonContext.Default.PublishResult));

    Environment.ExitCode = result.Success ? 0 : 1;
}
```

**Step 6: Remove WriteError helper if unused**

Delete if no longer used:

```csharp
private static void WriteError(string message)
{
    Console.Error.WriteLine(message);
}
```

**Step 7: Build to verify**

Run: `dotnet build`
Expected: Build succeeded

**Step 8: Run all tests**

Run: `dotnet test -v n`
Expected: PASS (no existing tests depend on publish recompile behavior)

**Step 9: Commit**

```bash
git add src/Commands/PublishCommand.cs
git commit -m "feat: remove -recompile flag from publish command"
```

---

## Task 7: Update README.md

**Files:**
- Modify: `README.md`

**Step 1: Update compile command quick-start example**

Find the "Compiling an App" section and update to show new options:

```markdown
### Compiling an App

```bash
bcdev compile \
  -appJsonPath "/path/to/app.json"

# With compiler options
bcdev compile \
  -appJsonPath "/path/to/app.json" \
  -parallel \
  -maxDegreeOfParallelism 4
```
```

**Step 2: Update publish command quick-start example**

Find the "Publishing an App" section and update (remove -recompile):

```markdown
### Publishing an App

```bash
bcdev publish \
  -appPath "/path/to/MyApp.app" \
  -launchJsonPath "/path/to/.vscode/launch.json" \
  -launchJsonName "Your Config Name" \
  -Username "bcuser" \
  -Password "bcpassword"
```
```

**Step 3: Update compile command table**

Replace the `bcdev compile` table with:

```markdown
### `bcdev compile`

Compile an AL application.

| Option | Required | Description |
|--------|----------|-------------|
| `-appJsonPath` | Yes | Path to app.json file |
| `-packageCachePath` | No | Path to .alpackages folder |
| `-suppressWarnings` | No | Suppress compiler warnings from output |
| `-generateReportLayout` | No | Generate report layout files |
| `-parallel` | No | Enable parallel compilation |
| `-maxDegreeOfParallelism` | No | Max concurrent compilation tasks |
| `-continueBuildOnError` | No | Continue building even if errors found |
```

**Step 4: Update publish command table**

Replace the `bcdev publish` table with:

```markdown
### `bcdev publish`

Publish an AL application to Business Central.

| Option | Required | Description |
|--------|----------|-------------|
| `-appPath` | Yes | Path to .app file |
| `-launchJsonPath` | Yes | Path to launch.json |
| `-launchJsonName` | Yes | Configuration name |
| `-Username` | Yes* | Username |
| `-Password` | Yes* | Password |

*Required for UserPassword auth
```

**Step 5: Commit**

```bash
git add README.md
git commit -m "docs: update compile and publish command documentation"
```

---

## Task 8: Final Verification

**Step 1: Run full test suite**

Run: `dotnet test -v n`
Expected: All tests pass

**Step 2: Build release**

Run: `dotnet build -c Release`
Expected: Build succeeded

**Step 3: Test CLI help output**

Run: `dotnet run --project src -- compile --help`
Expected: Shows new options (-generateReportLayout, -parallel, -maxDegreeOfParallelism, -continueBuildOnError)

Run: `dotnet run --project src -- publish --help`
Expected: Shows simplified options (no -recompile, -appJsonPath, -packageCachePath)

**Step 4: Final commit (if any uncommitted changes)**

```bash
git status
# If clean, done. Otherwise commit any remaining changes.
```
