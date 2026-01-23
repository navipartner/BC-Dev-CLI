# Configuration Guide

## launch.json Configuration

The bcdev CLI reads BC connection settings from VS Code's launch.json file. This file is typically located at `.vscode/launch.json` in your AL project.

### Sample launch.json

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "My BC Container",
      "type": "al",
      "request": "launch",
      "server": "http://bccontainer",
      "port": 7049,
      "serverInstance": "BC",
      "tenant": "default",
      "authentication": "UserPassword",
      "startupObjectId": 22,
      "startupObjectType": "Page",
      "schemaUpdateMode": "Synchronize"
    },
    {
      "name": "BC Cloud Sandbox",
      "type": "al",
      "request": "launch",
      "server": "https://businesscentral.dynamics.com",
      "port": 443,
      "serverInstance": "sandbox",
      "tenant": "your-tenant-id",
      "authentication": "AAD",
      "startupObjectId": 22,
      "startupObjectType": "Page"
    }
  ]
}
```

### Configuration Fields

| Field | Description | Default |
|-------|-------------|---------|
| `name` | Configuration name (used with `-launchJsonName`) | - |
| `server` | BC server URL | `http://localhost` |
| `port` | BC server port (typically 7049 for dev services) | `7049` |
| `serverInstance` | BC server instance name | `BC` |
| `tenant` | Tenant ID or name | `default` |
| `authentication` | Auth method: `UserPassword`, `AAD`, `MicrosoftEntraID` | `UserPassword` |
| `schemaUpdateMode` | Schema update mode for publishing | `Synchronize` |

## Authentication Methods

### UserPassword (NavUserPassword)

Standard username/password authentication for on-premises BC or Docker containers:

```bash
bcdev test \
  -launchJsonPath ".vscode/launch.json" \
  -launchJsonName "My BC Container" \
  -Username "admin" \
  -Password "secret"
```

### AAD (Azure Active Directory / Microsoft Entra ID)

For BC SaaS or AAD-enabled on-premises:

**Interactive (browser flow):**
```bash
bcdev test \
  -launchJsonPath ".vscode/launch.json" \
  -launchJsonName "BC Cloud Sandbox"
```

**Username/Password (for CI/CD):**
```bash
bcdev test \
  -launchJsonPath ".vscode/launch.json" \
  -launchJsonName "BC Cloud Sandbox" \
  -Username "user@tenant.onmicrosoft.com" \
  -Password "password"
```

## BC Client DLL

The CLI includes a bundled BC client DLL (`Microsoft.Dynamics.Framework.UI.Client.dll`) for BC28. If you need to use a different version:

```bash
bcdev test \
  -launchJsonPath ".vscode/launch.json" \
  -launchJsonName "My Config" \
  -Username "admin" \
  -Password "secret" \
  -bcClientDllPath "/path/to/your/Microsoft.Dynamics.Framework.UI.Client.dll"
```

## AL Compiler

The compile command requires the AL compiler (alc.exe). Common locations:

- **Windows (VS Code Extension):** `%USERPROFILE%\.vscode\extensions\ms-dynamics-smb.al-*\bin\alc.exe`
- **Windows (BC DVD):** `C:\Program Files (x86)\Microsoft Dynamics 365 Business Central\*\AL Development Environment\alc.exe`
- **Docker Container:** `/run/my-bc/alc/alc.exe`

```bash
bcdev compile \
  -appJsonPath "app.json" \
  -compilerPath "/path/to/alc.exe"
```

## Environment Variables

While bcdev doesn't read environment variables directly, you can use them in your shell scripts:

```bash
export BC_USERNAME="admin"
export BC_PASSWORD="secret"
export LAUNCH_JSON=".vscode/launch.json"
export CONFIG_NAME="My BC Container"

bcdev test \
  -launchJsonPath "$LAUNCH_JSON" \
  -launchJsonName "$CONFIG_NAME" \
  -Username "$BC_USERNAME" \
  -Password "$BC_PASSWORD"
```

## Timeouts

The default test timeout is 30 minutes. For long-running tests:

```bash
bcdev test \
  -launchJsonPath ".vscode/launch.json" \
  -launchJsonName "My Config" \
  -Username "admin" \
  -Password "secret" \
  -timeout 60  # 60 minutes
```
