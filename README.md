# bcdev - Business Central Development CLI

A cross-platform CLI tool for Microsoft Business Central development operations. Compile, publish, and test AL applications from any platform (macOS, Linux, Windows) without Windows-only PowerShell dependencies.

## Features

- **Compile** AL applications using the AL compiler (alc.exe)
- **Publish** apps to BC via Development Service REST API
- **Test** execution via BC's test tool page (130455)
- **Cross-platform** - works on macOS, Linux, and Windows
- **JSON output** - easy to parse, Claude-friendly output
- **Multiple auth methods** - UserPassword and Azure AD/Microsoft Entra ID

## Quick Start

### Installation

1. Download the latest release for your platform
2. Extract to a directory in your PATH
3. Run `bcdev --help` to verify installation

Or build from source:

```bash
cd src
dotnet build
dotnet run -- --help
```

### Running Tests

```bash
bcdev test \
  -launchJsonPath "/path/to/.vscode/launch.json" \
  -launchJsonName "Your Config Name" \
  -Username "bcuser" \
  -Password "bcpassword"
```

### Compiling an App

```bash
bcdev compile \
  -appJsonPath "/path/to/app.json" \
  -compilerPath "/path/to/alc.exe"
```

### Publishing an App

```bash
bcdev publish \
  -appPath "/path/to/MyApp.app" \
  -launchJsonPath "/path/to/.vscode/launch.json" \
  -launchJsonName "Your Config Name" \
  -Username "bcuser" \
  -Password "bcpassword"
```

## Commands

### `bcdev compile`

Compile an AL application.

| Option | Required | Description |
|--------|----------|-------------|
| `-appJsonPath` | Yes | Path to app.json file |
| `-compilerPath` | Yes | Path to alc.exe compiler |
| `-packageCachePath` | No | Path to .alpackages folder |

### `bcdev publish`

Publish an AL application to Business Central.

| Option | Required | Description |
|--------|----------|-------------|
| `-appPath` | Yes* | Path to .app file |
| `-recompile` | No | Compile before publishing |
| `-appJsonPath` | Yes** | Path to app.json (with -recompile) |
| `-compilerPath` | Yes** | Path to alc.exe (with -recompile) |
| `-launchJsonPath` | Yes | Path to launch.json |
| `-launchJsonName` | Yes | Configuration name |
| `-authType` | No | UserPassword or AAD |
| `-Username` | Yes*** | Username |
| `-Password` | Yes*** | Password |

*Required when not using -recompile
**Required with -recompile
***Required for UserPassword auth

### `bcdev test`

Run tests against Business Central.

| Option | Required | Description |
|--------|----------|-------------|
| `-launchJsonPath` | Yes | Path to launch.json |
| `-launchJsonName` | Yes | Configuration name |
| `-Username` | Yes* | Username |
| `-Password` | Yes* | Password |
| `-CodeunitId` | No | Specific test codeunit ID |
| `-MethodName` | No | Specific test method name |
| `-testSuite` | No | Test suite name (default: DEFAULT) |
| `-timeout` | No | Timeout in minutes (default: 30) |
| `-bcClientDllPath` | No | Custom BC client DLL path |

*Required for UserPassword auth

## Output Format

All commands output JSON to stdout:

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

Exit codes:
- `0` - Success
- `1` - Failure

## Requirements

- .NET 8 runtime (bundled in self-contained builds)
- BC Test Tool extension installed (Page 130455) for test command
- AL compiler (alc.exe) for compile command
- Network access to BC server for publish/test commands

## Building from Source

```bash
# Debug build
cd src
dotnet build

# Release build (self-contained)
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

# Other platforms
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

## License

MIT
