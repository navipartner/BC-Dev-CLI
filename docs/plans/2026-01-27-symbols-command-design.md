# bcdev symbols Command Design

## Overview

Add a `bcdev symbols` command to download symbol packages from a Business Central instance for compilation. Also fix AOT build warnings/errors in the release pipeline.

## Part 1: Symbols Command

### Command Interface

```
bcdev symbols
  -appJsonPath      (required)  Path to app.json file
  -launchJsonPath   (required)  Path to launch.json file  
  -launchJsonName   (required)  Configuration name in launch.json
  -packageCachePath (optional)  Output path, defaults to .alpackages/ next to app.json
  -Username         (optional)  Override username from launch.json
  -Password         (optional)  Override password from launch.json
```

**Example:**
```bash
bcdev symbols \
  -appJsonPath ./app.json \
  -launchJsonPath ./.vscode/launch.json \
  -launchJsonName test_container \
  -Username admin \
  -Password secret
```

### Symbol Resolution

**Symbols downloaded (in order):**

1. **Platform symbols** (from app.json `platform` and `application` fields):
   - `Microsoft/Application/{application version}` 
   - `Microsoft/System/{platform version}`
   - `Microsoft/Base Application/{application version}` (appId: `437dbf0e-84ff-417a-965d-ed2bb9650972`)

2. **Explicit dependencies** (from app.json `dependencies[]` array)

**URL format:**
```
GET {server}/{serverInstance}/dev/packages?publisher={publisher}&appName={name}&versionText={version}&tenant={tenant}
GET {server}/{serverInstance}/dev/packages?publisher={publisher}&appName={name}&versionText={version}&appId={id}&tenant={tenant}
```

**Output path logic:**
1. If `-packageCachePath` provided → use it
2. Else → use `.alpackages/` next to app.json
3. Create directory if missing

**File naming:** `{Publisher}_{Name}_{Version}.app`

### New Files
- `src/Commands/SymbolsCommand.cs` - Command definition
- `src/Services/SymbolService.cs` - Download logic

### Modified Files
- `src/Commands/PublishCommand.cs` - Remove legacy `-authType` parameter
- `src/Program.cs` - Register SymbolsCommand

### Output Format

```json
{
  "success": true,
  "outputPath": "/path/to/.alpackages",
  "downloadedSymbols": ["Microsoft_Application_27.0.0.0.app", ...],
  "failures": []
}
```

Exit code: 0 = success, 1 = failure

---

## Part 2: Fix AOT Build Warnings/Errors

### Critical Error Fix
**Problem:** `PublishAot and PublishSingleFile cannot be specified at the same time`
- Release workflow uses `-p:PublishAot=true`
- csproj has `<PublishSingleFile>true</PublishSingleFile>`

**Fix:** Remove `PublishSingleFile` and related properties from bcdev.csproj (AOT is preferred for releases)

### JSON Source Generation for AOT
**Problem:** JSON serialization/deserialization causes IL3050/IL2026 warnings

**Fix:** Create `JsonContext.cs` with source-generated serializers:
```csharp
[JsonSerializable(typeof(LaunchConfigurations))]
[JsonSerializable(typeof(AppJson))]
[JsonSerializable(typeof(CompileResult))]
[JsonSerializable(typeof(PublishResult))]
[JsonSerializable(typeof(TestResult))]
[JsonSerializable(typeof(SymbolsResult))]  // New
internal partial class JsonContext : JsonSerializerContext { }
```

Update all `JsonSerializer.Serialize/Deserialize` calls to use the generated context.

### Dynamic Type Warnings Suppression
**Problem:** BC client late-binding uses `dynamic` types (unavoidable for reflection-based DLL loading)

**Fix:** Add `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes to:
- `BCClientLoader` class methods
- `ClientContext` constructor and methods
- `TestRunner` methods that use dynamic types

This suppresses warnings while documenting the intentional use of reflection.

### Files to Modify
- `src/bcdev.csproj` - Remove PublishSingleFile settings
- `src/JsonContext.cs` - New file with source-generated JSON context
- `src/Services/LaunchConfigService.cs` - Use JsonContext
- `src/Services/CompilerService.cs` - Use JsonContext
- `src/Services/ArtifactService.cs` - Use JsonContext
- `src/Formatters/JsonResultFormatter.cs` - Use JsonContext
- `src/Commands/CompileCommand.cs` - Use JsonContext
- `src/Commands/PublishCommand.cs` - Use JsonContext, remove -authType
- `src/Commands/TestCommand.cs` - Use JsonContext
- `src/BC/BCClientLoader.cs` - Add suppression attributes
- `src/BC/ClientContext.cs` - Add suppression attributes
- `src/BC/TestRunner.cs` - Use JsonContext, add suppression attributes
