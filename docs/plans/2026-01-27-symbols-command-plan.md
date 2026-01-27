# Symbols Command & AOT Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add `bcdev symbols` command to download symbol packages from BC instances, and fix AOT build warnings/errors in the release pipeline.

**Architecture:** New SymbolService handles HTTP requests to BC `/dev/packages` endpoint. JSON source generation replaces reflection-based serialization for AOT compatibility. Dynamic type warnings suppressed with attributes on BC client code.

**Tech Stack:** .NET 8, System.CommandLine, System.Text.Json source generation, xUnit

---

## Task 1: Fix AOT/SingleFile Conflict in csproj

**Files:**
- Modify: `src/bcdev.csproj`

**Step 1: Remove PublishSingleFile settings**

Edit `src/bcdev.csproj` to remove the SingleFile properties that conflict with AOT:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>BCDev</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>bcdev</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    <PackageReference Include="System.ServiceModel.Primitives" Version="8.0.0" />
    <PackageReference Include="Microsoft.Identity.Client" Version="4.61.3" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

</Project>
```

**Step 2: Verify build still works**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/bcdev.csproj
git commit -m "fix: remove PublishSingleFile to allow AOT compilation"
```

---

## Task 2: Create JSON Source Generation Context

**Files:**
- Create: `src/JsonContext.cs`
- Modify: `src/Models/AppJson.cs` (add missing model)

**Step 1: Create the JsonContext file**

Create `src/JsonContext.cs`:

```csharp
using System.Text.Json.Serialization;
using BCDev.Formatters;
using BCDev.Models;
using BCDev.Services;

namespace BCDev;

/// <summary>
/// Source-generated JSON serializer context for AOT compatibility
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LaunchConfigurations))]
[JsonSerializable(typeof(LaunchConfiguration))]
[JsonSerializable(typeof(AppJson))]
[JsonSerializable(typeof(CompilerService.CompileResult))]
[JsonSerializable(typeof(PublishService.PublishResult))]
[JsonSerializable(typeof(TestService.TestResult))]
[JsonSerializable(typeof(TestService.TestMethodResult))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(SuccessResponse))]
[JsonSerializable(typeof(ArtifactService.BcArtifact))]
[JsonSerializable(typeof(ArtifactService.BcArtifact[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class JsonContext : JsonSerializerContext
{
}
```

**Step 2: Verify build compiles**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds (source generator runs)

**Step 3: Commit**

```bash
git add src/JsonContext.cs
git commit -m "feat: add JSON source generation context for AOT"
```

---

## Task 3: Update JsonResultFormatter to Use Source Generation

**Files:**
- Modify: `src/Formatters/JsonResultFormatter.cs`

**Step 1: Update JsonResultFormatter**

Replace `src/Formatters/JsonResultFormatter.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BCDev.Formatters;

/// <summary>
/// JSON formatter for consistent output across all commands
/// </summary>
public static class JsonResultFormatter
{
    /// <summary>
    /// Format an object as JSON for output using source-generated context
    /// </summary>
    public static string Format<T>(T obj, JsonTypeInfo<T> typeInfo)
    {
        return JsonSerializer.Serialize(obj, typeInfo);
    }

    /// <summary>
    /// Format and write to stdout using source-generated context
    /// </summary>
    public static void WriteToConsole<T>(T obj, JsonTypeInfo<T> typeInfo)
    {
        Console.WriteLine(Format(obj, typeInfo));
    }

    /// <summary>
    /// Create a standardized error response
    /// </summary>
    public static string FormatError(string message, string? details = null)
    {
        var error = new ErrorResponse
        {
            Success = false,
            Error = message,
            Details = details
        };
        return JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse);
    }

    /// <summary>
    /// Create a standardized success response
    /// </summary>
    public static string FormatSuccess(string message)
    {
        var response = new SuccessResponse
        {
            Success = true,
            Message = message
        };
        return JsonSerializer.Serialize(response, JsonContext.Default.SuccessResponse);
    }
}

/// <summary>
/// Standard error response structure
/// </summary>
public class ErrorResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// Standard success response structure
/// </summary>
public class SuccessResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
```

**Step 2: Verify build**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/Formatters/JsonResultFormatter.cs
git commit -m "refactor: update JsonResultFormatter to use source generation"
```

---

## Task 4: Update LaunchConfigService for AOT

**Files:**
- Modify: `src/Services/LaunchConfigService.cs`

**Step 1: Update LaunchConfigService**

Replace `src/Services/LaunchConfigService.cs`:

```csharp
using System.Text.Json;
using BCDev.Models;

namespace BCDev.Services;

/// <summary>
/// Service for parsing and managing launch.json configurations
/// </summary>
public class LaunchConfigService
{
    /// <summary>
    /// Load and parse launch.json file
    /// </summary>
    public LaunchConfigurations LoadLaunchJson(string launchJsonPath)
    {
        if (!File.Exists(launchJsonPath))
        {
            throw new FileNotFoundException($"Launch configuration file not found: {launchJsonPath}");
        }

        var json = File.ReadAllText(launchJsonPath);

        var configs = JsonSerializer.Deserialize(json, JsonContext.Default.LaunchConfigurations);
        if (configs == null)
        {
            throw new InvalidOperationException($"Failed to parse launch.json: {launchJsonPath}");
        }

        return configs;
    }

    /// <summary>
    /// Get a specific configuration by name
    /// </summary>
    public LaunchConfiguration GetConfiguration(string launchJsonPath, string configName)
    {
        var configs = LoadLaunchJson(launchJsonPath);

        var config = configs.Configurations.FirstOrDefault(c =>
            c.Name.Equals(configName, StringComparison.OrdinalIgnoreCase));

        if (config == null)
        {
            var availableNames = string.Join(", ", configs.Configurations.Select(c => c.Name));
            throw new InvalidOperationException(
                $"Configuration '{configName}' not found in launch.json. Available configurations: {availableNames}");
        }

        return config;
    }
}
```

**Step 2: Run unit tests**

Run: `dotnet test tests/bcdev.Tests.csproj --filter "FullyQualifiedName~LaunchConfigService"`
Expected: All tests pass

**Step 3: Commit**

```bash
git add src/Services/LaunchConfigService.cs
git commit -m "refactor: update LaunchConfigService to use source-generated JSON"
```

---

## Task 5: Update ArtifactService for AOT

**Files:**
- Modify: `src/Services/ArtifactService.cs`

**Step 1: Read current ArtifactService**

First examine the file to identify JSON deserialization locations.

**Step 2: Update JSON calls to use JsonContext**

Update all `JsonSerializer.Deserialize<T>` calls to use `JsonContext.Default.X` instead.

For `BcArtifact[]` deserialization around line 120:
```csharp
var artifacts = JsonSerializer.Deserialize(json, JsonContext.Default.BcArtifactArray);
```

For `AppJson` deserialization around line 149:
```csharp
var appJson = JsonSerializer.Deserialize(appJsonContent, JsonContext.Default.AppJson);
```

**Step 3: Verify build**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/Services/ArtifactService.cs
git commit -m "refactor: update ArtifactService to use source-generated JSON"
```

---

## Task 6: Update CompilerService for AOT

**Files:**
- Modify: `src/Services/CompilerService.cs`

**Step 1: Update JSON deserialization**

Update the `AppJson` deserialization around line 67:
```csharp
var appJson = JsonSerializer.Deserialize(appJsonContent, JsonContext.Default.AppJson);
```

**Step 2: Verify build**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/Services/CompilerService.cs
git commit -m "refactor: update CompilerService to use source-generated JSON"
```

---

## Task 7: Update Commands for AOT JSON Serialization

**Files:**
- Modify: `src/Commands/CompileCommand.cs`
- Modify: `src/Commands/PublishCommand.cs`
- Modify: `src/Commands/TestCommand.cs`

**Step 1: Update CompileCommand**

Replace the JSON serialization around line 53:
```csharp
Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.CompileResult));
```

**Step 2: Update PublishCommand - Remove authType and fix JSON**

Remove the `-authType` option and update JSON serialization:
```csharp
Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.PublishResult));
```

Also update the `GetCredentialProviderAsync` call to not pass `authType`:
```csharp
var credentialProvider = await GetCredentialProviderAsync(config, username, password);
```

And update the method signature in PublishService to remove the `authType` parameter.

**Step 3: Update TestCommand**

Replace the JSON serialization around line 135:
```csharp
Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.TestResult));
```

**Step 4: Run tests**

Run: `dotnet test tests/bcdev.Tests.csproj`
Expected: All tests pass

**Step 5: Commit**

```bash
git add src/Commands/CompileCommand.cs src/Commands/PublishCommand.cs src/Commands/TestCommand.cs
git commit -m "refactor: update commands to use source-generated JSON, remove legacy authType"
```

---

## Task 8: Update PublishService to Remove authType Parameter

**Files:**
- Modify: `src/Services/PublishService.cs`

**Step 1: Update PublishAsync signature**

Remove `AuthType? authType` parameter:
```csharp
public async Task<PublishResult> PublishAsync(
    bool recompile,
    string? appPath,
    string? appJsonPath,
    string? compilerPath,
    string? packageCachePath,
    string launchJsonPath,
    string launchJsonName,
    string? username,
    string? password,
    string? bcClientDllPath)
```

**Step 2: Update GetCredentialProviderAsync**

Remove authType parameter and use config.Authentication directly:
```csharp
private static Task<ICredentialProvider> GetCredentialProviderAsync(
    LaunchConfiguration config,
    string? username,
    string? password)
{
    if (config.Authentication == AuthenticationMethod.UserPassword)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "Username and password are required for UserPassword authentication");
        }
        return Task.FromResult<ICredentialProvider>(new NavUserPasswordProvider(username, password));
    }

    // AAD/MicrosoftEntraID auth
    var authority = !string.IsNullOrEmpty(config.PrimaryTenantDomain)
        ? $"https://login.microsoftonline.com/{config.PrimaryTenantDomain}"
        : "https://login.microsoftonline.com/common";
    var scopes = new[] { $"{config.GetServerForScope()}/.default" };
    return Task.FromResult<ICredentialProvider>(new AadAuthProvider(authority, scopes, username: username, password: password));
}
```

**Step 3: Verify build**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/Services/PublishService.cs
git commit -m "refactor: remove authType parameter from PublishService"
```

---

## Task 9: Add Suppression Attributes to BC Client Code

**Files:**
- Modify: `src/BC/BCClientLoader.cs`
- Modify: `src/BC/ClientContext.cs`
- Modify: `src/BC/TestRunner.cs`

**Step 1: Add attributes to BCClientLoader**

Add at top of file:
```csharp
using System.Diagnostics.CodeAnalysis;
```

Add attribute to class:
```csharp
[RequiresUnreferencedCode("BC client loading uses reflection for late binding")]
[RequiresDynamicCode("BC client loading uses dynamic code generation")]
public static class BCClientLoader
```

**Step 2: Add attributes to ClientContext**

Add attributes to constructor and key methods:
```csharp
[RequiresUnreferencedCode("BC client context uses reflection for late binding")]
[RequiresDynamicCode("BC client context uses dynamic code generation")]
public class ClientContext : IDisposable
```

**Step 3: Add attributes to TestRunner**

Add attributes to the class:
```csharp
[RequiresUnreferencedCode("TestRunner uses BC client which requires reflection")]
[RequiresDynamicCode("TestRunner uses BC client which requires dynamic code")]
public class TestRunner
```

Also update JSON deserialization around line 100 to use source generation:
```csharp
var testResults = JsonSerializer.Deserialize(jsonResult, JsonContext.Default.TestResult);
```

**Step 4: Verify build with no warnings**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds with significantly fewer warnings

**Step 5: Commit**

```bash
git add src/BC/BCClientLoader.cs src/BC/ClientContext.cs src/BC/TestRunner.cs
git commit -m "fix: add AOT suppression attributes to BC client code"
```

---

## Task 10: Create SymbolsResult Model

**Files:**
- Create: `src/Models/SymbolsResult.cs`
- Modify: `src/JsonContext.cs`

**Step 1: Create SymbolsResult model**

Create `src/Models/SymbolsResult.cs`:

```csharp
namespace BCDev.Models;

/// <summary>
/// Result of a symbols download operation
/// </summary>
public class SymbolsResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public List<string> DownloadedSymbols { get; set; } = new();
    public List<SymbolFailure> Failures { get; set; } = new();
}

/// <summary>
/// Information about a failed symbol download
/// </summary>
public class SymbolFailure
{
    public string Symbol { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
```

**Step 2: Add to JsonContext**

Add to `src/JsonContext.cs`:
```csharp
[JsonSerializable(typeof(SymbolsResult))]
[JsonSerializable(typeof(SymbolFailure))]
```

**Step 3: Verify build**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/Models/SymbolsResult.cs src/JsonContext.cs
git commit -m "feat: add SymbolsResult model for symbols command"
```

---

## Task 11: Create SymbolService - Unit Tests First

**Files:**
- Create: `tests/Unit/SymbolServiceTests.cs`
- Create: `tests/Fixtures/valid-app-with-deps.json`

**Step 1: Create test fixture**

Create `tests/Fixtures/valid-app-with-deps.json`:
```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Test App",
  "publisher": "TestPublisher",
  "version": "1.0.0.0",
  "platform": "27.0.0.0",
  "application": "27.0.0.0",
  "dependencies": [
    {
      "id": "437dbf0e-84ff-417a-965d-ed2bb9650972",
      "name": "Base Application",
      "publisher": "Microsoft",
      "version": "27.0.0.0"
    }
  ]
}
```

**Step 2: Create unit tests**

Create `tests/Unit/SymbolServiceTests.cs`:

```csharp
using BCDev.Models;
using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class SymbolServiceTests
{
    private readonly string _fixturesPath;

    public SymbolServiceTests()
    {
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    }

    [Fact]
    public void GetSymbolsToDownload_ReturnsApplicationSystemAndBaseApp()
    {
        var appJson = new AppJson
        {
            Platform = "27.0.0.0",
            Application = "27.0.0.0",
            Dependencies = new List<AppDependency>()
        };

        var symbols = SymbolService.GetSymbolsToDownload(appJson);

        Assert.Contains(symbols, s => s.Name == "Application" && s.Publisher == "Microsoft");
        Assert.Contains(symbols, s => s.Name == "System" && s.Publisher == "Microsoft");
        Assert.Contains(symbols, s => s.Name == "Base Application" && s.Publisher == "Microsoft");
    }

    [Fact]
    public void GetSymbolsToDownload_IncludesDependencies()
    {
        var appJson = new AppJson
        {
            Platform = "27.0.0.0",
            Application = "27.0.0.0",
            Dependencies = new List<AppDependency>
            {
                new() { Id = "test-id", Name = "Custom App", Publisher = "Custom", Version = "1.0.0.0" }
            }
        };

        var symbols = SymbolService.GetSymbolsToDownload(appJson);

        Assert.Contains(symbols, s => s.Name == "Custom App" && s.Publisher == "Custom");
    }

    [Fact]
    public void GetSymbolFileName_FormatsCorrectly()
    {
        var result = SymbolService.GetSymbolFileName("Microsoft", "Base Application", "27.0.0.0");

        Assert.Equal("Microsoft_Base Application_27.0.0.0.app", result);
    }

    [Fact]
    public void BuildPackageUrl_OnPrem_FormatsCorrectly()
    {
        var config = new LaunchConfiguration
        {
            Server = "https://localhost",
            Port = 7049,
            ServerInstance = "BC",
            Tenant = "default"
        };

        var url = SymbolService.BuildPackageUrl(config, "Microsoft", "Application", "27.0.0.0", null);

        Assert.Contains("/dev/packages?", url);
        Assert.Contains("publisher=Microsoft", url);
        Assert.Contains("appName=Application", url);
        Assert.Contains("versionText=27.0.0.0", url);
        Assert.Contains("tenant=default", url);
    }

    [Fact]
    public void BuildPackageUrl_WithAppId_IncludesAppId()
    {
        var config = new LaunchConfiguration
        {
            Server = "https://localhost",
            Port = 7049,
            ServerInstance = "BC",
            Tenant = "default"
        };

        var url = SymbolService.BuildPackageUrl(config, "Microsoft", "Base Application", "27.0.0.0", "437dbf0e-84ff-417a-965d-ed2bb9650972");

        Assert.Contains("appId=437dbf0e-84ff-417a-965d-ed2bb9650972", url);
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/bcdev.Tests.csproj --filter "FullyQualifiedName~SymbolService"`
Expected: FAIL (SymbolService does not exist)

**Step 4: Commit test file**

```bash
git add tests/Unit/SymbolServiceTests.cs tests/Fixtures/valid-app-with-deps.json
git commit -m "test: add unit tests for SymbolService"
```

---

## Task 12: Implement SymbolService

**Files:**
- Create: `src/Services/SymbolService.cs`

**Step 1: Create SymbolService**

Create `src/Services/SymbolService.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BCDev.Auth;
using BCDev.BC;
using BCDev.Models;

namespace BCDev.Services;

/// <summary>
/// Service for downloading symbol packages from Business Central
/// </summary>
public class SymbolService
{
    /// <summary>
    /// Represents a symbol to download
    /// </summary>
    public class SymbolInfo
    {
        public string Publisher { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? AppId { get; set; }
    }

    /// <summary>
    /// Download symbols for an AL application
    /// </summary>
    public async Task<SymbolsResult> DownloadSymbolsAsync(
        string appJsonPath,
        string launchJsonPath,
        string launchJsonName,
        string? packageCachePath,
        string? username,
        string? password)
    {
        var result = new SymbolsResult();

        try
        {
            // Parse app.json
            var appJsonContent = await File.ReadAllTextAsync(appJsonPath);
            var appJson = JsonSerializer.Deserialize(appJsonContent, JsonContext.Default.AppJson);
            if (appJson == null)
            {
                throw new InvalidOperationException($"Failed to parse app.json: {appJsonPath}");
            }

            // Determine output path
            var appDir = Path.GetDirectoryName(Path.GetFullPath(appJsonPath))!;
            var outputPath = packageCachePath ?? Path.Combine(appDir, ".alpackages");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            result.OutputPath = outputPath;

            // Load launch configuration
            var launchConfigService = new LaunchConfigService();
            var config = launchConfigService.GetConfiguration(launchJsonPath, launchJsonName);

            // Get credentials
            var credentialProvider = GetCredentialProvider(config, username, password);
            var credentials = await credentialProvider.GetCredentialsAsync();

            // Disable SSL verification for dev environments
            SslVerification.Disable();

            // Get list of symbols to download
            var symbols = GetSymbolsToDownload(appJson);

            // Download each symbol
            using var httpClient = CreateHttpClient(config, credentials, credentialProvider.AuthenticationScheme);
            
            foreach (var symbol in symbols)
            {
                var fileName = GetSymbolFileName(symbol.Publisher, symbol.Name, symbol.Version);
                var filePath = Path.Combine(outputPath, fileName);

                try
                {
                    var url = BuildPackageUrl(config, symbol.Publisher, symbol.Name, symbol.Version, symbol.AppId);
                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(filePath, content);
                        result.DownloadedSymbols.Add(fileName);
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        result.Failures.Add(new SymbolFailure
                        {
                            Symbol = $"{symbol.Publisher}_{symbol.Name}_{symbol.Version}",
                            Error = $"HTTP {(int)response.StatusCode} - {errorContent}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add(new SymbolFailure
                    {
                        Symbol = $"{symbol.Publisher}_{symbol.Name}_{symbol.Version}",
                        Error = ex.Message
                    });
                }
            }

            result.Success = result.Failures.Count == 0;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Failures.Add(new SymbolFailure
            {
                Symbol = "initialization",
                Error = ex.Message
            });
        }

        return result;
    }

    /// <summary>
    /// Get list of symbols to download based on app.json
    /// </summary>
    public static List<SymbolInfo> GetSymbolsToDownload(AppJson appJson)
    {
        var symbols = new List<SymbolInfo>();

        // Platform symbols
        var platformVersion = appJson.Platform ?? "1.0.0.0";
        var applicationVersion = appJson.Application ?? platformVersion;

        // Application symbol
        symbols.Add(new SymbolInfo
        {
            Publisher = "Microsoft",
            Name = "Application",
            Version = applicationVersion
        });

        // System symbol
        symbols.Add(new SymbolInfo
        {
            Publisher = "Microsoft",
            Name = "System",
            Version = platformVersion
        });

        // Base Application symbol (with known app ID)
        symbols.Add(new SymbolInfo
        {
            Publisher = "Microsoft",
            Name = "Base Application",
            Version = applicationVersion,
            AppId = "437dbf0e-84ff-417a-965d-ed2bb9650972"
        });

        // Explicit dependencies from app.json
        if (appJson.Dependencies != null)
        {
            foreach (var dep in appJson.Dependencies)
            {
                // Skip if already added (e.g., Base Application)
                if (symbols.Any(s => s.Name == dep.Name && s.Publisher == dep.Publisher))
                    continue;

                symbols.Add(new SymbolInfo
                {
                    Publisher = dep.Publisher,
                    Name = dep.Name,
                    Version = dep.Version,
                    AppId = string.IsNullOrEmpty(dep.Id) ? null : dep.Id
                });
            }
        }

        return symbols;
    }

    /// <summary>
    /// Generate symbol file name
    /// </summary>
    public static string GetSymbolFileName(string publisher, string name, string version)
    {
        return $"{publisher}_{name}_{version}.app";
    }

    /// <summary>
    /// Build the package download URL
    /// </summary>
    public static string BuildPackageUrl(LaunchConfiguration config, string publisher, string name, string version, string? appId)
    {
        var baseUrl = config.GetDevServicesUrl().TrimEnd('/');
        var tenant = config.Tenant ?? "default";

        var queryParams = new List<string>
        {
            $"publisher={Uri.EscapeDataString(publisher)}",
            $"appName={Uri.EscapeDataString(name)}",
            $"versionText={Uri.EscapeDataString(version)}"
        };

        if (!string.IsNullOrEmpty(appId))
        {
            queryParams.Add($"appId={Uri.EscapeDataString(appId)}");
        }

        queryParams.Add($"tenant={Uri.EscapeDataString(tenant)}");

        return $"{baseUrl}packages?{string.Join("&", queryParams)}";
    }

    private static HttpClient CreateHttpClient(LaunchConfiguration config, ICredentials credentials, string authScheme)
    {
        var handler = new HttpClientHandler
        {
            Credentials = credentials,
            PreAuthenticate = true,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        var devServiceUrl = config.GetDevServicesUrl();

        if (authScheme == "AzureActiveDirectory" && credentials is TokenCredential tokenCred)
        {
            var networkCred = tokenCred.GetCredential(new Uri(devServiceUrl), "Bearer");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", networkCred.Password);
        }
        else if (credentials is NetworkCredential netCred)
        {
            var authBytes = Encoding.ASCII.GetBytes($"{netCred.UserName}:{netCred.Password}");
            var authBase64 = Convert.ToBase64String(authBytes);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authBase64);
        }

        return client;
    }

    private static ICredentialProvider GetCredentialProvider(
        LaunchConfiguration config,
        string? username,
        string? password)
    {
        if (config.Authentication == AuthenticationMethod.UserPassword)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    "Username and password are required for UserPassword authentication");
            }
            return new NavUserPasswordProvider(username, password);
        }

        // AAD/MicrosoftEntraID auth
        var authority = !string.IsNullOrEmpty(config.PrimaryTenantDomain)
            ? $"https://login.microsoftonline.com/{config.PrimaryTenantDomain}"
            : "https://login.microsoftonline.com/common";
        var scopes = new[] { $"{config.GetServerForScope()}/.default" };
        return new AadAuthProvider(authority, scopes, username: username, password: password);
    }
}
```

**Step 2: Run unit tests**

Run: `dotnet test tests/bcdev.Tests.csproj --filter "FullyQualifiedName~SymbolService"`
Expected: All tests pass

**Step 3: Commit**

```bash
git add src/Services/SymbolService.cs
git commit -m "feat: implement SymbolService for downloading BC symbols"
```

---

## Task 13: Create SymbolsCommand

**Files:**
- Create: `src/Commands/SymbolsCommand.cs`
- Modify: `src/Program.cs`

**Step 1: Create SymbolsCommand**

Create `src/Commands/SymbolsCommand.cs`:

```csharp
using System.CommandLine;
using System.Text.Json;
using BCDev.Services;

namespace BCDev.Commands;

public static class SymbolsCommand
{
    public static Command Create()
    {
        var command = new Command("symbols", "Download symbol packages from Business Central");

        var appJsonPathOption = new Option<string>(
            name: "-appJsonPath",
            description: "Path to app.json file")
        {
            IsRequired = true
        };

        var launchJsonPathOption = new Option<string>(
            name: "-launchJsonPath",
            description: "Path to launch.json file")
        {
            IsRequired = true
        };

        var launchJsonNameOption = new Option<string>(
            name: "-launchJsonName",
            description: "Configuration name in launch.json")
        {
            IsRequired = true
        };

        var packageCachePathOption = new Option<string?>(
            name: "-packageCachePath",
            description: "Path to output folder (defaults to .alpackages next to app.json)");

        var usernameOption = new Option<string?>(
            name: "-Username",
            description: "Username for authentication");

        var passwordOption = new Option<string?>(
            name: "-Password",
            description: "Password for authentication");

        command.AddOption(appJsonPathOption);
        command.AddOption(launchJsonPathOption);
        command.AddOption(launchJsonNameOption);
        command.AddOption(packageCachePathOption);
        command.AddOption(usernameOption);
        command.AddOption(passwordOption);

        command.SetHandler(async (context) =>
        {
            var appJsonPath = context.ParseResult.GetValueForOption(appJsonPathOption)!;
            var launchJsonPath = context.ParseResult.GetValueForOption(launchJsonPathOption)!;
            var launchJsonName = context.ParseResult.GetValueForOption(launchJsonNameOption)!;
            var packageCachePath = context.ParseResult.GetValueForOption(packageCachePathOption);
            var username = context.ParseResult.GetValueForOption(usernameOption);
            var password = context.ParseResult.GetValueForOption(passwordOption);

            await ExecuteAsync(appJsonPath, launchJsonPath, launchJsonName, packageCachePath, username, password);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string appJsonPath,
        string launchJsonPath,
        string launchJsonName,
        string? packageCachePath,
        string? username,
        string? password)
    {
        var symbolService = new SymbolService();
        var result = await symbolService.DownloadSymbolsAsync(
            appJsonPath, launchJsonPath, launchJsonName, packageCachePath, username, password);

        Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.SymbolsResult));

        Environment.ExitCode = result.Success ? 0 : 1;
    }
}
```

**Step 2: Register command in Program.cs**

Update `src/Program.cs`:

```csharp
using System.CommandLine;
using BCDev.Commands;

namespace BCDev;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("BC Dev CLI - Cross-platform tool for Business Central development operations");

        // Add compile command
        rootCommand.AddCommand(CompileCommand.Create());

        // Add publish command
        rootCommand.AddCommand(PublishCommand.Create());

        // Add test command
        rootCommand.AddCommand(TestCommand.Create());

        // Add symbols command
        rootCommand.AddCommand(SymbolsCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}
```

**Step 3: Verify build**

Run: `dotnet build src/bcdev.csproj`
Expected: Build succeeds

**Step 4: Verify CLI shows symbols command**

Run: `dotnet run --project src/bcdev.csproj -- --help`
Expected: Shows "symbols" in command list

**Step 5: Commit**

```bash
git add src/Commands/SymbolsCommand.cs src/Program.cs
git commit -m "feat: add symbols command to CLI"
```

---

## Task 14: Run All Tests and Final Verification

**Files:** None (verification only)

**Step 1: Run all unit tests**

Run: `dotnet test tests/bcdev.Tests.csproj`
Expected: All tests pass

**Step 2: Verify AOT build**

Run: `dotnet publish src/bcdev.csproj -c Release -r linux-x64 -p:PublishAot=true --nologo`
Expected: Build succeeds with minimal warnings (only dynamic type warnings which are suppressed)

**Step 3: Verify CLI help**

Run: `dotnet run --project src/bcdev.csproj -- symbols --help`
Expected: Shows all options for symbols command

**Step 4: Final commit with version bump (if needed)**

```bash
git add -A
git commit -m "feat: complete symbols command implementation with AOT fixes"
```

---

## Summary

**New files created:**
- `src/JsonContext.cs` - JSON source generation
- `src/Models/SymbolsResult.cs` - Result model
- `src/Services/SymbolService.cs` - Download logic
- `src/Commands/SymbolsCommand.cs` - CLI command
- `tests/Unit/SymbolServiceTests.cs` - Unit tests
- `tests/Fixtures/valid-app-with-deps.json` - Test fixture

**Modified files:**
- `src/bcdev.csproj` - Remove PublishSingleFile
- `src/Formatters/JsonResultFormatter.cs` - Source generation
- `src/Services/LaunchConfigService.cs` - Source generation
- `src/Services/ArtifactService.cs` - Source generation
- `src/Services/CompilerService.cs` - Source generation
- `src/Services/PublishService.cs` - Remove authType
- `src/Commands/CompileCommand.cs` - Source generation
- `src/Commands/PublishCommand.cs` - Remove authType, source generation
- `src/Commands/TestCommand.cs` - Source generation
- `src/BC/BCClientLoader.cs` - Suppression attributes
- `src/BC/ClientContext.cs` - Suppression attributes
- `src/BC/TestRunner.cs` - Suppression attributes, source generation
- `src/Program.cs` - Register SymbolsCommand
