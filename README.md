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
  -recompile \
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

## TODO

Make the project compile even without any compiler or client .dll from MS.
The tests should trigger the download from MS in the next TODO point so they should be done together.

A -version parameter that is used to download the correct client context .dll and compiler from MS, if not already downloaded and cached. If not specified, the app.json platform version should be used.
It's important that the steps here are returned in stdout, so the user can see if its choosing to download the binaries from MS, which version, or if it re-used caches ones from disk, just to make it more transparent.

Polishing the three examples in the top of the README.md file

Making sure the command options list in the README.md file are all up to date

Making a release pipeline that stores the artifacts on GitHub releases.
It should be triggered by a new release tag on master branch.

Making sure the release uses AOT and bundled runtime compilation flags, so it's a single executable even though itll be bigger.

