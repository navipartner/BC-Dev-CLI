# Suppress Pseudo-Warnings Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the `-suppressWarnings` flag truly suppress all warnings by filtering them from raw compiler output (StdOut/StdErr), not just from the parsed Warnings list.

**Architecture:** Filter all warning lines from StdOut/StdErr when `suppressWarnings` is true. The AL compiler (alc.exe) doesn't support global warning suppression, so we handle it at the output level.

**Tech Stack:** C#, .NET 10, xUnit for testing

---

## Background

When `-suppressWarnings` is passed to the compile command, warnings are filtered from the parsed `Warnings` list but still appear in the raw `StdOut`/`StdErr` output. These raw warnings are then displayed by consuming tools (like Claude Code).

The fix is simple: filter warning lines from the raw output when `suppressWarnings` is true.

---

### Task 1: Add Tests for Warning Output Filtering

**Files:**
- Create: `tests/Unit/WarningSuppressionTests.cs`

**Step 1: Create test file with comprehensive tests**

```csharp
using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class WarningSuppressionTests
{
    [Fact]
    public void FilterWarningsFromOutput_RemovesWarningLines()
    {
        var input = "src/app.al(10,5): warning AL0432: Method 'Foo' is marked for removal.\n" +
                    "src/app.al(15,1): error AL0118: 'Bar' is not a valid identifier\n" +
                    "src/other.al(20,3): warning AL0433: Field 'Baz' is obsolete.\n" +
                    "Compilation finished.";

        var filtered = CompilerService.FilterWarningsFromOutput(input);

        Assert.DoesNotContain(": warning ", filtered);
        Assert.Contains(": error ", filtered);
        Assert.Contains("Compilation finished.", filtered);
    }

    [Fact]
    public void FilterWarningsFromOutput_HandlesMultipleWarningTypes()
    {
        var input = "file.al(1,1): warning AL0432: Deprecation warning\n" +
                    "file.al(2,1): warning AL0433: Another warning\n" +
                    "file.al(3,1): error AL0001: An error\n" +
                    "file.al(4,1): warning AL1234: Yet another warning";

        var filtered = CompilerService.FilterWarningsFromOutput(input);
        var lines = filtered.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
        Assert.Contains("error AL0001", lines[0]);
    }

    [Fact]
    public void FilterWarningsFromOutput_PreservesEmptyAndNullInput()
    {
        Assert.Equal("", CompilerService.FilterWarningsFromOutput(""));
        Assert.Null(CompilerService.FilterWarningsFromOutput(null!));
    }

    [Fact]
    public void FilterWarningsFromOutput_PreservesNonWarningLines()
    {
        var input = "Microsoft (R) AL Compiler version 14.0.12345.0\n" +
                    "Copyright (C) Microsoft Corporation. All rights reserved.\n" +
                    "\n" +
                    "Compilation started...\n" +
                    "file.al(1,1): error AL0001: Error message\n" +
                    "Compilation failed.";

        var filtered = CompilerService.FilterWarningsFromOutput(input);

        Assert.Equal(input, filtered);
    }

    [Fact]
    public void FilterWarningsFromOutput_HandlesWindowsLineEndings()
    {
        var input = "file.al(1,1): warning AL0432: A warning\r\n" +
                    "file.al(2,1): error AL0001: An error\r\n" +
                    "Done.";

        var filtered = CompilerService.FilterWarningsFromOutput(input);

        Assert.DoesNotContain(": warning ", filtered);
        Assert.Contains(": error ", filtered);
        Assert.Contains("Done.", filtered);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/bcdev.Tests.csproj --filter "WarningSuppressionTests"`
Expected: FAIL - `FilterWarningsFromOutput` method does not exist

**Step 3: Commit failing tests**

```bash
git add tests/Unit/WarningSuppressionTests.cs
git commit -m "test: add failing tests for warning output filtering"
```

---

### Task 2: Implement FilterWarningsFromOutput Method

**Files:**
- Modify: `src/Services/CompilerService.cs`

**Step 1: Add the static filter method**

Add this method to `CompilerService` class (after `ParseCompilerLine` method, around line 238):

```csharp
/// <summary>
/// Filter warning lines from compiler output text.
/// Handles both Unix (\n) and Windows (\r\n) line endings.
/// </summary>
public static string? FilterWarningsFromOutput(string? output)
{
    if (string.IsNullOrEmpty(output))
        return output;

    var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    var filteredLines = lines.Where(line => !line.Contains(": warning ")).ToArray();
    return string.Join("\n", filteredLines);
}
```

**Step 2: Run tests to verify they pass**

Run: `dotnet test tests/bcdev.Tests.csproj --filter "WarningSuppressionTests"`
Expected: PASS

**Step 3: Commit implementation**

```bash
git add src/Services/CompilerService.cs
git commit -m "feat: add FilterWarningsFromOutput method"
```

---

### Task 3: Apply Filter to StdOut/StdErr When suppressWarnings Is True

**Files:**
- Modify: `src/Services/CompilerService.cs`

**Step 1: Update CompileAsync to filter raw output**

In `CompileAsync` method, find lines 123-128:

```csharp
result.ExitCode = process.ExitCode;
result.StdOut = stdout;
result.StdErr = stderr;

// Parse compiler output for errors and warnings
ParseCompilerOutput(stdout + stderr, result, suppressWarnings);
```

Change to:

```csharp
result.ExitCode = process.ExitCode;

// Filter warnings from raw output if suppressing
if (suppressWarnings)
{
    result.StdOut = FilterWarningsFromOutput(stdout);
    result.StdErr = FilterWarningsFromOutput(stderr);
}
else
{
    result.StdOut = stdout;
    result.StdErr = stderr;
}

// Parse compiler output for errors and warnings
ParseCompilerOutput(stdout + stderr, result, suppressWarnings);
```

**Step 2: Run all tests**

Run: `dotnet test tests/bcdev.Tests.csproj`
Expected: PASS

**Step 3: Commit**

```bash
git add src/Services/CompilerService.cs
git commit -m "feat: filter warnings from StdOut/StdErr when suppressWarnings is true"
```

---

### Task 4: Final Verification

**Step 1: Run full test suite**

Run: `dotnet test tests/bcdev.Tests.csproj`
Expected: All tests pass

**Step 2: Build release configuration**

Run: `dotnet build src/bcdev.csproj -c Release`
Expected: Build succeeded

**Step 3: Verify no warnings/errors from build**

Check output for any compiler warnings or errors.

---

## Summary

After completing these tasks, the `-suppressWarnings` flag will:

1. **Filter warning lines from `StdOut`/`StdErr`** - consuming tools won't see them
2. **Continue to exclude warnings from the `Warnings` list** - existing behavior preserved
3. **Work for ALL warning codes** - no hardcoded list to maintain

Total changes: ~15 lines of implementation code + ~60 lines of tests.
