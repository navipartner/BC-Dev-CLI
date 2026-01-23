# BC Dev CLI Skill

A cross-platform CLI tool for Business Central development operations including compiling, publishing, and testing AL applications.

## Commands

### Compile

Compile an AL application using alc.exe:

```bash
bcdev compile -appJsonPath "/path/to/app.json" -compilerPath "/path/to/alc.exe"
```

**Required Options:**
- `-appJsonPath`: Path to the app.json file
- `-compilerPath`: Path to the AL compiler (alc.exe)

**Optional:**
- `-packageCachePath`: Path to .alpackages folder (defaults to .alpackages in app folder)

**Output:** JSON with compilation result, errors, and warnings.

### Publish

Publish an AL application to Business Central:

```bash
# Publish pre-compiled app
bcdev publish -appPath "/path/to/app.app" -launchJsonPath "/path/to/launch.json" -launchJsonName "MyConfig" -Username "user" -Password "pass"

# Compile and publish
bcdev publish -recompile -appJsonPath "/path/to/app.json" -compilerPath "/path/to/alc.exe" -launchJsonPath "/path/to/launch.json" -launchJsonName "MyConfig" -Username "user" -Password "pass"
```

**Required Options:**
- `-launchJsonPath`: Path to VS Code launch.json file
- `-launchJsonName`: Name of the configuration in launch.json
- Either `-appPath` (for pre-compiled) or `-recompile` with `-appJsonPath` and `-compilerPath`

**Auth Options:**
- `-authType`: Authentication type (UserPassword or AAD)
- `-Username`: Username for UserPassword auth
- `-Password`: Password for UserPassword auth

**Output:** JSON with publish result and any errors.

### Test

Run tests against Business Central:

```bash
# Run all tests
bcdev test -launchJsonPath "/path/to/launch.json" -launchJsonName "MyConfig" -Username "user" -Password "pass"

# Run specific codeunit
bcdev test -launchJsonPath "/path/to/launch.json" -launchJsonName "MyConfig" -Username "user" -Password "pass" -CodeunitId 84003

# Run specific test method
bcdev test -launchJsonPath "/path/to/launch.json" -launchJsonName "MyConfig" -Username "user" -Password "pass" -CodeunitId 84003 -MethodName "TestSomething"
```

**Required Options:**
- `-launchJsonPath`: Path to VS Code launch.json file
- `-launchJsonName`: Name of the configuration in launch.json
- `-Username`: Username for UserPassword auth
- `-Password`: Password for UserPassword auth

**Optional:**
- `-CodeunitId`: Run tests from specific codeunit only
- `-MethodName`: Run specific test method only
- `-testSuite`: Test suite name (default: DEFAULT)
- `-timeout`: Timeout in minutes (default: 30)
- `-bcClientDllPath`: Custom path to BC client DLL

**Output:** JSON with test results:
```json
{
  "success": true,
  "totalTests": 10,
  "passed": 9,
  "failed": 1,
  "skipped": 0,
  "duration": "00:01:23",
  "results": [
    {
      "codeunit": "MyTestCodeunit",
      "function": "TestSomething",
      "result": "Pass",
      "duration": "00:00:05"
    }
  ]
}
```

## Authentication

The CLI supports two authentication methods:

1. **UserPassword** (NavUserPassword): Username and password credentials passed via CLI
2. **AAD** (Azure AD / Microsoft Entra ID): Interactive or username/password flow

For AAD, if no credentials are provided, an interactive browser flow will open.

## launch.json Configuration

The CLI reads BC connection settings from VS Code's launch.json:

```json
{
  "configurations": [
    {
      "name": "MyConfig",
      "server": "http://bcserver",
      "port": 7049,
      "serverInstance": "BC",
      "tenant": "default",
      "authentication": "UserPassword"
    }
  ]
}
```

## Error Handling

All commands return JSON with:
- `success`: Boolean indicating success/failure
- `message`: Human-readable message
- `error`: Error details (when failed)

Exit codes:
- 0: Success
- 1: Failure

## Requirements

- .NET 8 runtime (bundled in self-contained builds)
- BC Test Tool extension installed (Page 130455) for test command
- AL compiler (alc.exe) for compile command
- Network access to BC server for publish/test commands
