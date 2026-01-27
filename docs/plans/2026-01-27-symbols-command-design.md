# bcdev symbols Command Design

## Overview

Add a `bcdev symbols` command to download symbol packages from a Business Central instance for compilation.

## Command Interface

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

## Symbol Resolution

### Symbols downloaded (in order):

1. **Platform symbols** (from app.json `platform` and `application` fields):
   - `Microsoft/Application/{application version}` 
   - `Microsoft/System/{platform version}`
   - `Microsoft/Base Application/{application version}` (appId: `437dbf0e-84ff-417a-965d-ed2bb9650972`)

2. **Explicit dependencies** (from app.json `dependencies[]` array)

### URL format:
```
GET {server}/{serverInstance}/dev/packages?publisher={publisher}&appName={name}&versionText={version}&tenant={tenant}
GET {server}/{serverInstance}/dev/packages?publisher={publisher}&appName={name}&versionText={version}&appId={id}&tenant={tenant}
```

### Output path logic:
1. If `-packageCachePath` provided → use it
2. Else → use `.alpackages/` next to app.json
3. Create directory if missing

### File naming:
`{Publisher}_{Name}_{Version}.app` (matches AL extension convention)

## Architecture

### New files:
- `src/Commands/SymbolsCommand.cs` - Command definition
- `src/Services/SymbolService.cs` - Download logic

### Modified files:
- `src/Commands/PublishCommand.cs` - Remove legacy `-authType` parameter
- `src/Program.cs` - Register SymbolsCommand

### Authentication:
- `UserPassword` → Basic auth via `NavUserPasswordProvider`
- `MicrosoftEntraID` → Bearer token via `AadAuthProvider` (structured for SaaS, focus on on-prem)

### HTTP approach:
- Use `HttpClient` directly (not BC client DLL)
- Reuse `SslVerification.Disable()` for dev environments

## Output Format

### Success:
```json
{
  "success": true,
  "outputPath": "/path/to/.alpackages",
  "downloadedSymbols": [
    "Microsoft_Application_27.0.0.0.app",
    "Microsoft_System_27.0.0.0.app",
    "Microsoft_Base Application_27.0.0.0.app"
  ],
  "failures": []
}
```

### Partial failure:
```json
{
  "success": false,
  "outputPath": "/path/to/.alpackages",
  "downloadedSymbols": ["Microsoft_Application_27.0.0.0.app"],
  "failures": [
    {
      "symbol": "Microsoft_System_27.0.0.0",
      "error": "HTTP 404 - Package not found"
    }
  ]
}
```

### Exit codes:
- `0` - All symbols downloaded
- `1` - One or more failures

## Error Handling

- Invalid app.json / launch.json paths
- Missing configuration name
- Authentication failure (401/403)
- Package not found (404)
- Network/connection errors
