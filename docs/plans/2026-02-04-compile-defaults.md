# Compile Command Defaults Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Set default values for compile command: parallel=true, maxDegreeOfParallelism=4, generateReportLayout=false

**Architecture:** Change the CLI option definitions in CompileCommand.cs from nullable types with no defaults to non-nullable types with explicit defaults. The CompilerService layer remains unchanged since defaults are applied at CLI parsing.

**Tech Stack:** .NET 8, System.CommandLine, xUnit

---

### Task 1: Update Unit Tests for Default Behavior

**Files:**
- Modify: `tests/Unit/CompilerServiceTests.cs`

**Step 1: Write test for default arguments**

Add this test to verify the expected default values will produce correct compiler flags:

```csharp
[Fact]
public void BuildArguments_WithDefaults_ProducesExpectedFlags()
{
    // These are the new defaults: parallel=true, maxDegreeOfParallelism=4, generateReportLayout=false
    var args = CompilerService.BuildCompilerArguments(
        projectPath: "/project",
        outputPath: "/out.app",
        packageCachePath: null,
        generateReportLayout: false,
        parallel: true,
        maxDegreeOfParallelism: 4,
        continueBuildOnError: null);

    Assert.Contains("/parallel+", args);
    Assert.Contains("/maxdegreeofparallelism:4", args);
    Assert.Contains("/generatereportlayout-", args);
    Assert.DoesNotContain("/continuebuildonerror", args);
}
```

**Step 2: Run test to verify it passes**

Run: `dotnet test tests/Unit/CompilerServiceTests.cs --filter "BuildArguments_WithDefaults_ProducesExpectedFlags" -v n`
Expected: PASS (the BuildCompilerArguments method already supports these values)

**Step 3: Commit**

```bash
git add tests/Unit/CompilerServiceTests.cs
git commit -m "test: add test verifying compile command default argument generation"
```

---

### Task 2: Update CompileCommand Option Definitions

**Files:**
- Modify: `src/Commands/CompileCommand.cs:28-38`

**Step 1: Change generateReportLayout from bool? to bool with default false**

Replace lines 28-30:
```csharp
var generateReportLayoutOption = new Option<bool>(
    name: "-generateReportLayout",
    description: "Generate report layout files during compilation",
    getDefaultValue: () => false);
```

**Step 2: Change parallel from bool? to bool with default true**

Replace lines 32-34:
```csharp
var parallelOption = new Option<bool>(
    name: "-parallel",
    description: "Enable parallel compilation",
    getDefaultValue: () => true);
```

**Step 3: Change maxDegreeOfParallelism from int? to int with default 4**

Replace lines 36-38:
```csharp
var maxDegreeOfParallelismOption = new Option<int>(
    name: "-maxDegreeOfParallelism",
    description: "Maximum number of concurrent compilation tasks",
    getDefaultValue: () => 4);
```

**Step 4: Update the handler to use non-nullable types**

Replace lines 57-59:
```csharp
var generateReportLayout = context.ParseResult.GetValueForOption(generateReportLayoutOption);
var parallel = context.ParseResult.GetValueForOption(parallelOption);
var maxDegreeOfParallelism = context.ParseResult.GetValueForOption(maxDegreeOfParallelismOption);
```

**Step 5: Update validation - remove null check since int is no longer nullable**

Replace lines 62-68:
```csharp
// Validate maxDegreeOfParallelism
if (maxDegreeOfParallelism <= 0)
{
    Console.Error.WriteLine("Error: -maxDegreeOfParallelism must be greater than 0");
    context.ExitCode = 1;
    return;
}
```

**Step 6: Update ExecuteAsync signature and call**

Replace line 70-71:
```csharp
await ExecuteAsync(appJsonPath, packageCachePath, suppressWarnings,
    generateReportLayout, parallel, maxDegreeOfParallelism, continueBuildOnError);
```

Replace lines 77-84:
```csharp
private static async Task ExecuteAsync(
    string appJsonPath,
    string? packageCachePath,
    bool suppressWarnings,
    bool generateReportLayout,
    bool parallel,
    int maxDegreeOfParallelism,
    bool? continueBuildOnError)
```

Replace lines 94-96:
```csharp
var result = await compilerService.CompileAsync(
    appJsonPath, compilerPath, packageCachePath, suppressWarnings,
    generateReportLayout, parallel, maxDegreeOfParallelism, continueBuildOnError);
```

**Step 7: Build to verify no compilation errors**

Run: `dotnet build`
Expected: Build succeeded

**Step 8: Run all unit tests**

Run: `dotnet test tests/Unit -v n`
Expected: All tests pass

**Step 9: Commit**

```bash
git add src/Commands/CompileCommand.cs
git commit -m "feat: set compile command defaults - parallel=true, maxDegreeOfParallelism=4, generateReportLayout=false"
```

---

### Task 3: Verify Integration Tests Still Pass

**Files:**
- None (verification only)

**Step 1: Run integration tests**

Run: `dotnet test tests/Integration --filter "Category!=Slow" -v n`
Expected: All non-slow integration tests pass

Note: Full integration tests require BC artifacts and may be slow. The unit tests provide sufficient coverage for the default value changes.

---

## Summary of Changes

| File | Change |
|------|--------|
| `src/Commands/CompileCommand.cs` | Change `parallel`, `maxDegreeOfParallelism`, `generateReportLayout` from nullable to non-nullable with defaults |
| `tests/Unit/CompilerServiceTests.cs` | Add test for default argument generation |

## Default Values

| Parameter | Old Default | New Default |
|-----------|-------------|-------------|
| parallel | null (no flag) | true (`/parallel+`) |
| maxDegreeOfParallelism | null (no flag) | 4 (`/maxdegreeofparallelism:4`) |
| generateReportLayout | null (no flag) | false (`/generatereportlayout-`) |
