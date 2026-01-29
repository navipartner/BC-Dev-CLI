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

### Package Naming Convention (Verified via API)

**Feed Base URLs:**
- MSSymbols flat container: `https://dynamicssmb2.pkgs.visualstudio.com/571e802d-b44b-45fc-bd41-4cfddec73b44/_packaging/b656b10c-3de0-440c-900c-bc2e4e86d84c/nuget/v3/flat2/`
- AppSourceSymbols flat container: `https://dynamicssmb2.pkgs.visualstudio.com/571e802d-b44b-45fc-bd41-4cfddec73b44/_packaging/3f253fc9-be40-4eb5-b0e5-1a277ee0ed60/nuget/v3/flat2/`

**Package ID patterns:**

| Package Type | W1 (country-less) | Country-specific |
|--------------|-------------------|------------------|
| Platform | `microsoft.platform.symbols` | N/A |
| Application | `microsoft.application.symbols` | `microsoft.application.{country}.symbols` |
| BaseApplication | `microsoft.baseapplication.symbols.{appId}` | `microsoft.baseapplication.{country}.symbols.{appId}` |
| SystemApplication | `microsoft.systemapplication.symbols.{appId}` | `microsoft.systemapplication.{country}.symbols.{appId}` |
| AppSource ISV | `{publisher}.{name}.symbols.{appId}` | N/A (no country variants) |

**Version format:**
- Platform: 3-part (`27.0.45024`)
- All others: 4-part (`27.3.44313.45043`)

**Available countries:** AT, AU, BE, CA, CH, CZ, DE, DK, ES, FI, FR, GB, IN, IS, IT, MX, NL, NO, NZ, SE, US

**Package contents:**
```
manifest.nuspec (or {PackageId}.nuspec)
{Publisher}_{Name}_{Version}.app
```
No subfolders - country is part of package ID, not internal structure.

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

## Out of Scope: RAD (Rapid Application Development)

RAD was investigated but determined to be out of scope for this CLI.

### Why RAD Won't Work in a CLI

RAD is a **VS Code extension feature**, not a compiler feature:

| Component | What it does |
|-----------|--------------|
| VS Code extension | Tracks file changes in editor, generates `rad.json` |
| `rad.json` | Lists Added/Modified/Removed objects since last publish |
| RAD publish | Sends only changed objects to BC server |
| `alc.exe` | Has no RAD flags - always does full compilation |

Key issues:
- No `alc.exe` command-line flags for incremental/delta compilation
- `rad.json` is generated by VS Code extension's file change tracking, not the compiler
- BC's publish endpoint for delta objects is internal to VS Code's protocol
- `syncMode` parameter (Add/Clean/ForceSync) is for schema sync, not delta publishing

Building our own delta tracking would require:
1. Persistent state tracking (file hashes)
2. Reverse-engineering VS Code's publish protocol
3. Risk of breaking with BC updates

**Recommendation**: Focus on NuGet symbols which provides clear value without these risks.

## References

- [AL-Go Settings](https://github.com/microsoft/AL-Go/blob/main/Scenarios/settings.md)
- [BcNuGet Package Format RFC](https://github.com/microsoft/AL-Go/discussions/301)
- [GenerateBcNuGet](https://github.com/BusinessCentralApps/GenerateBcNuGet)
- [Microsoft Release Plan](https://learn.microsoft.com/en-us/dynamics365/release-plan/2024wave2/smb/dynamics365-business-central/remove-friction-when-working-external-app-dependencies)
- [RAD Publishing Documentation](https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/developer/devenv-rad-publishing)
