# Design: Add Compiler Parameters & Remove Recompile Flag

## Overview

Two changes to bcdev CLI:
1. Add AL compiler parameters to the `compile` command that match VS Code `al.compilationOptions` settings
2. Remove the `-recompile` flag from the `publish` command

## Background

### Compiler Parameters

The AL compiler (`alc.exe`) supports these command-line parameters that correspond to VS Code settings:

| VS Code Setting | alc.exe Parameter | Format |
|-----------------|-------------------|--------|
| `generateReportLayout` | `/generatereportlayout[+\|-]` | Boolean flag |
| `parallel` | `/parallel[+\|-]` | Boolean flag |
| `maxDegreeOfParallelism` | `/maxdegreeofparallelism:<n>` | Integer |
| `continueBuildOnError` | `/continuebuildonerror[+\|-]` | Boolean flag |

Note: `incrementalBuild` is a VS Code extension setting (controls background compilation) with no alc.exe equivalent - excluded from this implementation.

### Recompile Flag Removal

The `-recompile` flag in the publish command was a convenience that compiled before publishing. Removing it:
- Simplifies the publish command
- Gives users full control over compiler options by running `compile` then `publish`
- Reduces code duplication between compile and publish commands

## Design

### 1. New CompileCommand Options

Add 4 nullable options to `CompileCommand.cs`:

```csharp
var generateReportLayoutOption = new Option<bool?>(
    name: "-generateReportLayout",
    description: "Generate report layout files (default: compiler default, true)");

var parallelOption = new Option<bool?>(
    name: "-parallel",
    description: "Enable parallel compilation (default: compiler default, true)");

var maxDegreeOfParallelismOption = new Option<int?>(
    name: "-maxDegreeOfParallelism",
    description: "Maximum concurrent compilation tasks (default: compiler default, 2)");

var continueBuildOnErrorOption = new Option<bool?>(
    name: "-continueBuildOnError",
    description: "Continue building even if errors are found (default: false)");
```

Design decisions:
- Nullable types so parameters are only passed when explicitly specified (preserves alc.exe defaults)
- camelCase names matching VS Code settings.json property names
- All optional (no `IsRequired = true`)

### 2. CompilerService Changes

Expand `CompileAsync` signature:

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

Build arguments conditionally:

```csharp
if (generateReportLayout.HasValue)
    args.Add($"/generatereportlayout{(generateReportLayout.Value ? "+" : "-")}");

if (parallel.HasValue)
    args.Add($"/parallel{(parallel.Value ? "+" : "-")}");

if (maxDegreeOfParallelism.HasValue)
    args.Add($"/maxdegreeofparallelism:{maxDegreeOfParallelism.Value}");

if (continueBuildOnError.HasValue)
    args.Add($"/continuebuildonerror{(continueBuildOnError.Value ? "+" : "-")}");
```

### 3. PublishCommand Changes

Remove from `PublishCommand.cs`:
- `-recompile` option
- `-appJsonPath` option
- `-packageCachePath` option
- Conditional validation logic

Make `-appPath` required:

```csharp
var appPathOption = new Option<string>(
    name: "-appPath",
    description: "Path to .app file")
{
    IsRequired = true
};
```

### 4. PublishService Changes

Simplify `PublishAsync` signature:

```csharp
public async Task<PublishResult> PublishAsync(
    string appPath,
    string launchJsonPath,
    string launchJsonName,
    string? username,
    string? password)
```

Remove:
- `recompile` parameter
- `appJsonPath` parameter
- `packageCachePath` parameter
- Recompile code block (lines 44-65)

### 5. README.md Updates

Update the Commands section:

**compile command table** - add new options:
| Option | Required | Description |
|--------|----------|-------------|
| `-appJsonPath` | Yes | Path to app.json file |
| `-packageCachePath` | No | Path to .alpackages folder |
| `-generateReportLayout` | No | Generate report layout files |
| `-parallel` | No | Enable parallel compilation |
| `-maxDegreeOfParallelism` | No | Max concurrent compilation tasks |
| `-continueBuildOnError` | No | Continue building even if errors found |

**publish command table** - simplify:
| Option | Required | Description |
|--------|----------|-------------|
| `-appPath` | Yes | Path to .app file |
| `-launchJsonPath` | Yes | Path to launch.json |
| `-launchJsonName` | Yes | Configuration name |
| `-Username` | Yes* | Username |
| `-Password` | Yes* | Password |

Update quick-start example to show `compile` then `publish` workflow.

## Files Changed

| File | Changes |
|------|---------|
| `src/Commands/CompileCommand.cs` | Add 4 new options, wire to handler |
| `src/Services/CompilerService.cs` | Expand `CompileAsync` signature, build new args |
| `src/Commands/PublishCommand.cs` | Remove recompile-related options, make appPath required |
| `src/Services/PublishService.cs` | Simplify `PublishAsync` signature, remove recompile logic |
| `README.md` | Update compile and publish command documentation |

## Design Decisions

1. **Breaking change handling**: Just remove `-recompile` without deprecation. This is a dev CLI and users can adapt by running `compile` then `publish`.

2. **CLI option casing**: Use camelCase for new options (`-generateReportLayout`) to match VS Code `settings.json` property names, even though existing auth options use PascalCase (`-Username`). The distinction makes sense: compiler options mirror VS Code settings, auth options are standalone.

3. **Validation**: Validate `maxDegreeOfParallelism > 0` in CompileCommand before calling the service. No special handling for `parallel=false` + `maxDegreeOfParallelism` - let alc.exe handle that interaction.

4. **Compiler version compatibility**: Not gated. If an older compiler doesn't support a flag, it will fail with a clear error from alc.exe itself.

## Testing

- Existing tests using `CompilerService.CompileAsync` continue to work (new params have defaults)
- Tests using `PublishService.PublishAsync` need signature updates
- Unit test argument building logic for new compiler options

## Sources

- [AL Language extension configuration - Microsoft Learn](https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/developer/devenv-al-extension-configuration)
- [Business Central: AL Compiler - Dan Kinsella](https://dankinsella.blog/business-central-al-compiler/)
