using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BCDev.Auth;
using BCDev.Models;

namespace BCDev.Services;

/// <summary>
/// Service for downloading symbol packages from Business Central or NuGet feeds
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
    /// Download symbols from NuGet feeds (default mode)
    /// </summary>
    public async Task<SymbolsResult> DownloadFromNuGetAsync(
        string appJsonPath,
        string? packageCachePath,
        string country = "w1")
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

            // Use NuGet feed service
            using var nugetService = new NuGetFeedService();
            return await nugetService.DownloadSymbolsAsync(appJson, outputPath, country);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Failures.Add(new SymbolFailure
            {
                Symbol = "initialization",
                Error = ex.Message
            });
            return result;
        }
    }

    /// <summary>
    /// Download symbols from Business Central server (legacy mode with -fromServer flag)
    /// </summary>
    public async Task<SymbolsResult> DownloadFromServerAsync(
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
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                    if (response.IsSuccessStatusCode)
                    {
                        // Stream directly to disk to avoid buffering large files in memory
                        await using var source = await response.Content.ReadAsStreamAsync();
                        await using var destination = File.Create(filePath);
                        await source.CopyToAsync(destination);
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

        // System symbol (maps to Platform package in NuGet)
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
            AppId = NuGetFeedService.BaseApplicationAppId
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
    /// Build the package download URL for BC server
    /// </summary>
    public static string BuildPackageUrl(LaunchConfiguration config, string publisher, string name, string version, string? appId)
    {
        var baseUrl = config.GetDevServicesUrl();
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }
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
