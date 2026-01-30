# bcdev - Business Central Development CLI

A CLI tool for Business Central developers and their agents. 
Compile, publish, and test AL applications from any platform (macOS, Linux, Windows).
Works the same against BC SaaS sandboxes, hosted dev environments like Alpaca or your own local containers - no need for BCContainerHelper or remote powershell on the BC server. 

### Running Tests

```bash
bcdev test \
  -launchJsonPath "/path/to/.vscode/launch.json" \
  -launchJsonName "Your Config Name" \
  -Username "bcuser" \
  -Password "bcpassword" \
  -CodeunitId 50100 \
  -MethodName "TestSalesOrderCreation"
```

### Compiling an App

```bash
bcdev compile \
  -appJsonPath "/path/to/app.json"
```

### Publishing an App

```bash
bcdev publish \
  -recompile \
  -appPath "/path/to/MyApp.app" \
  -launchJsonPath "/path/to/.vscode/launch.json" \
  -launchJsonName "Your Config Name" \
  -Username "bcuser" \
  -Password "bcpassword"
```

### Downloading Symbols

**NuGet mode (default):**
```bash
# Download symbols from NuGet feeds (no BC server required)
bcdev symbols -appJsonPath "/path/to/app.json"

# With country-specific packages (e.g., US, DE, DK)
bcdev symbols -appJsonPath "/path/to/app.json" -country us
```

**Server mode (opt-in):**
```bash
bcdev symbols \
  -appJsonPath "/path/to/app.json" \
  -fromServer \
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
| `-packageCachePath` | No | Path to .alpackages folder |

### `bcdev publish`

Publish an AL application to Business Central.

| Option | Required | Description |
|--------|----------|-------------|
| `-appPath` | Yes* | Path to .app file |
| `-recompile` | No | Compile before publishing |
| `-appJsonPath` | Yes** | Path to app.json (with -recompile) |
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

### `bcdev symbols`

Download symbol packages for compilation dependencies. By default, downloads from Microsoft's public NuGet feeds (faster, works offline/CI). Optionally download from a BC server with `-fromServer`.

| Option | Required | Description |
|--------|----------|-------------|
| `-appJsonPath` | Yes | Path to app.json file |
| `-packageCachePath` | No | Output folder (defaults to .alpackages next to app.json) |
| `-country` | No | Country code for localized symbols (e.g., us, de, dk). Default `w1` uses country-less packages |
| `-fromServer` | No | Download from BC server instead of NuGet feeds |
| `-launchJsonPath` | No* | Path to launch.json |
| `-launchJsonName` | No* | Configuration name |
| `-Username` | No** | Username |
| `-Password` | No** | Password |

*Required with `-fromServer`
**Required for UserPassword auth with `-fromServer`
