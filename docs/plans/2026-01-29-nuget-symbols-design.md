# NuGet Symbols Download Feature Design

## Overview

Add support for downloading AL symbols directly from Microsoft's public NuGet feeds, enabling faster downloads and offline/CI scenarios without requiring a running BC instance.

## Motivation

- **Speed**: Direct NuGet downloads are faster than BC server API
- **Offline/CI**: Works without a live BC environment
- **Parity**: Matches new VS Code AL extension capabilities (BC28+)

## Command Interface

### Default behavior (NuGet mode)

```bash
# Basic usage - only app.json required
bcdev symbols -appJsonPath ./app.json

# With country localization
bcdev symbols -appJsonPath ./app.json -country us

# Custom output directory
bcdev symbols -appJsonPath ./app.json -packageCachePath ./symbols
```

### BC Server mode (opt-in)

```bash
bcdev symbols -appJsonPath ./app.json -launchJsonPath ./.vscode/launch.json -fromServer
```

### Options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `-appJsonPath` | Yes | - | Path to app.json |
| `-packageCachePath` | No | `.alpackages` | Output directory |
| `-country` | No | `w1` | Country/region code for localized symbols |
| `-fromServer` | No | `false` | Use BC instance instead of NuGet |
| `-launchJsonPath` | Only with `-fromServer` | - | Path to launch.json |
| `-Username` / `-Password` | Only with `-fromServer` | - | Credentials for BC |

## NuGet Feed Integration

### Feed URLs

| Feed | URL | Purpose |
|------|-----|---------|
| MSSymbols | `https://dynamicssmb2.pkgs.visualstudio.com/DynamicsBCPublicFeeds/_packaging/MSSymbols/nuget/v3/index.json` | System, Application, Base Application |
| AppSourceSymbols | `https://dynamicssmb2.pkgs.visualstudio.com/DynamicsBCPublicFeeds/_packaging/AppSourceSymbols/nuget/v3/index.json` | ISV/partner app symbols |

### Package Resolution

1. Parse `app.json` to extract:
   - `platform` version -> `Microsoft.Platform` package
   - `application` version -> `Microsoft.Application` package
   - `dependencies` array -> each dependency by AppId

2. For each required package:
   - Query MSSymbols feed first (Microsoft apps)
   - Fall back to AppSourceSymbols feed (ISV apps)
   - Use NuGet V3 API for package discovery

3. Download `.nupkg`, extract `.app` file

### Package Naming Convention

- Microsoft packages: `microsoft.{name}.{appId}`
- Example: `microsoft.application.437dbf0e-84ff-417a-965d-ed2bb9650972`
- Country-specific content in subfolders: `/us/`, `/dk/`, `/w1/`

## Implementation Architecture

### New Components

```
src/
  Services/
    SymbolService.cs          # Modify: route between NuGet and BC server
    NuGetFeedService.cs       # NEW: NuGet feed queries and downloads
  Models/
    NuGetPackageInfo.cs       # NEW: NuGet package metadata
```

### NuGetFeedService Responsibilities

- Query NuGet V3 API for package metadata
- Resolve package versions matching app.json requirements
- Download `.nupkg` files
- Extract `.app` files (nupkg is a zip archive)
- Handle country-specific subfolder selection

### SymbolService Changes

- Add `fromServer` parameter to `DownloadSymbolsAsync`
- Route to `NuGetFeedService` by default
- Route to existing BC server logic when `fromServer = true`

### Dependencies

No new NuGet packages required:
- `HttpClient` for API calls (already available)
- `System.IO.Compression.ZipArchive` for .nupkg extraction (built into .NET)

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Package not found in MSSymbols | Try AppSourceSymbols feed |
| Package not found in either feed | Fail with error listing missing AppId |
| Country subfolder not found | Fall back to `w1/` or root |
| Network failure | Retry once, then fail with HTTP details |
| Version mismatch | Fail with error listing requested vs available versions |

## Output Format

Maintains existing `SymbolsResult` JSON structure:

```json
{
  "success": true,
  "outputPath": ".alpackages",
  "downloadedSymbols": ["Microsoft_System_24.0.0.0.app", ...],
  "failedSymbols": []
}
```

## References

- [AL-Go Settings](https://github.com/microsoft/AL-Go/blob/main/Scenarios/settings.md)
- [BcNuGet Package Format RFC](https://github.com/microsoft/AL-Go/discussions/301)
- [GenerateBcNuGet](https://github.com/BusinessCentralApps/GenerateBcNuGet)
- [Microsoft Release Plan](https://learn.microsoft.com/en-us/dynamics365/release-plan/2024wave2/smb/dynamics365-business-central/remove-friction-when-working-external-app-dependencies)
