using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using BCDev.Models;

namespace BCDev.Services;

/// <summary>
/// Service for downloading symbols from Microsoft's public NuGet feeds
/// </summary>
public class NuGetFeedService : IDisposable
{
    // Feed base URLs (flat container endpoints for direct package access)
    public const string MSSymbolsFeedBase = "https://dynamicssmb2.pkgs.visualstudio.com/571e802d-b44b-45fc-bd41-4cfddec73b44/_packaging/b656b10c-3de0-440c-900c-bc2e4e86d84c/nuget/v3/flat2";
    public const string AppSourceSymbolsFeedBase = "https://dynamicssmb2.pkgs.visualstudio.com/571e802d-b44b-45fc-bd41-4cfddec73b44/_packaging/3f253fc9-be40-4eb5-b0e5-1a277ee0ed60/nuget/v3/flat2";

    // Well-known AppIds for Microsoft packages
    public const string BaseApplicationAppId = "437dbf0e-84ff-417a-965d-ed2bb9650972";
    public const string SystemApplicationAppId = "63ca2fa4-4f03-4f2b-a480-172fef340d3f";
    public const string BusinessFoundationAppId = "f3552374-a1f2-4356-848e-196002525837";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public NuGetFeedService(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// Download symbols for an AL application from NuGet feeds
    /// </summary>
    public async Task<SymbolsResult> DownloadSymbolsAsync(
        AppJson appJson,
        string outputPath,
        string country = "w1")
    {
        var result = new SymbolsResult { OutputPath = outputPath };

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var symbols = SymbolService.GetSymbolsToDownload(appJson);

        foreach (var symbol in symbols)
        {
            try
            {
                var downloaded = await DownloadSymbolAsync(symbol, outputPath, country);
                if (downloaded != null)
                {
                    result.DownloadedSymbols.Add(downloaded);
                }
                else
                {
                    result.Failures.Add(new SymbolFailure
                    {
                        Symbol = $"{symbol.Publisher}_{symbol.Name}_{symbol.Version}",
                        Error = "Package not found in any feed"
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
        return result;
    }

    /// <summary>
    /// Download a single symbol package
    /// </summary>
    private async Task<string?> DownloadSymbolAsync(
        SymbolService.SymbolInfo symbol,
        string outputPath,
        string country)
    {
        // Try MSSymbols feed first, then AppSourceSymbols
        var feeds = new[] { MSSymbolsFeedBase, AppSourceSymbolsFeedBase };
        string? lastError = null;

        foreach (var feedBase in feeds)
        {
            var packageId = BuildPackageId(symbol, country, feedBase == AppSourceSymbolsFeedBase);
            var (versions, error) = await GetPackageVersionsAsync(feedBase, packageId);

            if (error != null)
            {
                lastError = error;
                continue;
            }

            if (versions == null || versions.Count == 0)
            {
                // For country-specific packages, try without country
                if (country != "w1")
                {
                    var countrylessPackageId = BuildPackageId(symbol, "w1", feedBase == AppSourceSymbolsFeedBase);
                    if (countrylessPackageId != packageId)
                    {
                        var (countrylessVersions, countrylessError) = await GetPackageVersionsAsync(feedBase, countrylessPackageId);
                        if (countrylessError != null)
                        {
                            lastError = countrylessError;
                        }
                        else if (countrylessVersions != null && countrylessVersions.Count > 0)
                        {
                            versions = countrylessVersions;
                            packageId = countrylessPackageId;
                        }
                    }
                }
            }

            if (versions == null || versions.Count == 0)
            {
                continue;
            }

            // Find exact matching version (strict)
            var matchedVersion = FindMatchingVersion(versions, symbol.Version);
            if (matchedVersion == null)
            {
                throw new InvalidOperationException(
                    $"Version {symbol.Version} not found for {packageId}. Available versions: {FormatAvailableVersions(versions)}");
            }

            // Download and extract
            var appFileName = await DownloadAndExtractPackageAsync(
                feedBase, packageId, matchedVersion, outputPath);

            return appFileName;
        }

        // If we had an error during feed queries, report it
        if (lastError != null)
        {
            throw new InvalidOperationException($"Failed to query feeds: {lastError}");
        }

        return null;
    }

    /// <summary>
    /// Build the NuGet package ID for a symbol
    /// </summary>
    public static string BuildPackageId(SymbolService.SymbolInfo symbol, string country, bool isAppSource)
    {
        var publisher = symbol.Publisher.ToLowerInvariant().Replace(" ", "");
        var name = symbol.Name.ToLowerInvariant().Replace(" ", "");
        // Normalize country to handle case variations and whitespace (e.g., "W1", " w1 ")
        var normalizedCountry = country.Trim().ToLowerInvariant();
        var countryUpper = normalizedCountry.ToUpperInvariant();
        var useCountry = normalizedCountry != "w1";

        // Special handling for Microsoft packages
        if (publisher == "microsoft")
        {
            // Platform has no country or AppId
            if (name == "system")
            {
                return "microsoft.platform.symbols";
            }

            // Application package (no AppId in package name)
            if (name == "application")
            {
                return useCountry
                    ? $"microsoft.application.{countryUpper}.symbols"
                    : "microsoft.application.symbols";
            }

            // BaseApplication, SystemApplication, etc. have AppId
            if (name == "baseapplication" || name == "base application")
            {
                var appId = symbol.AppId ?? BaseApplicationAppId;
                return useCountry
                    ? $"microsoft.baseapplication.{countryUpper}.symbols.{appId}"
                    : $"microsoft.baseapplication.symbols.{appId}";
            }

            if (name == "systemapplication" || name == "system application")
            {
                var appId = symbol.AppId ?? SystemApplicationAppId;
                return useCountry
                    ? $"microsoft.systemapplication.{countryUpper}.symbols.{appId}"
                    : $"microsoft.systemapplication.symbols.{appId}";
            }

            if (name == "businessfoundation" || name == "business foundation")
            {
                var appId = symbol.AppId ?? BusinessFoundationAppId;
                return $"microsoft.businessfoundation.symbols.{appId}";
            }

            // Other Microsoft packages - try with AppId if available
            if (!string.IsNullOrEmpty(symbol.AppId))
            {
                return useCountry
                    ? $"microsoft.{name}.{countryUpper}.symbols.{symbol.AppId}"
                    : $"microsoft.{name}.symbols.{symbol.AppId}";
            }

            return useCountry
                ? $"microsoft.{name}.{countryUpper}.symbols"
                : $"microsoft.{name}.symbols";
        }

        // AppSource ISV packages: {publisher}.{name}.symbols.{appId}
        if (!string.IsNullOrEmpty(symbol.AppId))
        {
            return $"{publisher}.{name}.symbols.{symbol.AppId}";
        }

        return $"{publisher}.{name}.symbols";
    }

    /// <summary>
    /// Get available versions for a package from the flat container
    /// </summary>
    public async Task<(List<string>? Versions, string? Error)> GetPackageVersionsAsync(string feedBase, string packageId)
    {
        var url = $"{feedBase}/{packageId.ToLowerInvariant()}/index.json";

        try
        {
            using var response = await _httpClient.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (null, null); // Package doesn't exist - not an error
            }
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"HTTP {(int)response.StatusCode} from {url}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var versionIndex = JsonSerializer.Deserialize(content, JsonContext.Default.NuGetVersionIndex);
            return (versionIndex?.Versions, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Network error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return (null, $"Invalid response from feed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (null, "Request timed out");
        }
    }

    /// <summary>
    /// Find matching version from available versions.
    /// Priority: exact match > closest higher version in same major.minor
    /// </summary>
    public static string? FindMatchingVersion(List<string> availableVersions, string targetVersion)
    {
        if (availableVersions.Count == 0) return null;

        // Try exact match first
        var exact = availableVersions.FirstOrDefault(v =>
            v.Equals(targetVersion, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Parse target version
        var targetParts = ParseVersionParts(targetVersion);
        if (targetParts == null) return null;

        // Find closest higher version in same major.minor
        string? bestMatch = null;
        (int major, int minor, int build, int rev)? bestParts = null;

        foreach (var version in availableVersions)
        {
            var parts = ParseVersionParts(version);
            if (parts == null) continue;

            // Must match major.minor
            if (parts.Value.major != targetParts.Value.major ||
                parts.Value.minor != targetParts.Value.minor)
                continue;

            // Must be >= target build
            if (parts.Value.build < targetParts.Value.build)
                continue;

            // If same build, revision must be >= target
            if (parts.Value.build == targetParts.Value.build &&
                parts.Value.rev < targetParts.Value.rev)
                continue;

            // Is this better (lower) than current best?
            if (bestParts == null ||
                parts.Value.build < bestParts.Value.build ||
                (parts.Value.build == bestParts.Value.build && parts.Value.rev < bestParts.Value.rev))
            {
                bestMatch = version;
                bestParts = parts;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Parse version string into parts. Handles 3-part (27.0.45024) and 4-part (27.0.45024.0) formats.
    /// </summary>
    private static (int major, int minor, int build, int rev)? ParseVersionParts(string version)
    {
        var segments = version.Split('.');
        if (segments.Length < 3) return null;

        if (!int.TryParse(segments[0], out var major)) return null;
        if (!int.TryParse(segments[1], out var minor)) return null;
        if (!int.TryParse(segments[2], out var build)) return null;

        var rev = 0;
        if (segments.Length >= 4)
        {
            int.TryParse(segments[3], out rev);
        }

        return (major, minor, build, rev);
    }

    /// <summary>
    /// Get a formatted list of available versions for error messages
    /// </summary>
    public static string FormatAvailableVersions(List<string> versions, int maxCount = 10)
    {
        if (versions.Count == 0)
        {
            return "(none)";
        }

        var display = versions.Take(maxCount).ToList();
        var result = string.Join(", ", display);
        if (versions.Count > maxCount)
        {
            result += $" (and {versions.Count - maxCount} more)";
        }
        return result;
    }

    /// <summary>
    /// Download a package and extract the .app file
    /// </summary>
    private async Task<string> DownloadAndExtractPackageAsync(
        string feedBase,
        string packageId,
        string version,
        string outputPath)
    {
        var packageIdLower = packageId.ToLowerInvariant();
        var url = $"{feedBase}/{packageIdLower}/{version}/{packageIdLower}.{version}.nupkg";

        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        // Find the .app file in the archive
        var appEntry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase));

        if (appEntry == null)
        {
            throw new InvalidOperationException($"No .app file found in package {packageId} {version}");
        }

        var outputFilePath = Path.Combine(outputPath, appEntry.Name);

        // Extract to a temp file first, then move atomically
        var tempFilePath = outputFilePath + ".tmp";
        try
        {
            await using (var entryStream = appEntry.Open())
            await using (var fileStream = File.Create(tempFilePath))
            {
                await entryStream.CopyToAsync(fileStream);
            }

            // Atomic move
            File.Move(tempFilePath, outputFilePath, overwrite: true);
        }
        finally
        {
            // Clean up temp file if it still exists
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }

        return appEntry.Name;
    }
}

/// <summary>
/// NuGet flat container version index response
/// </summary>
public class NuGetVersionIndex
{
    [JsonPropertyName("versions")]
    public List<string> Versions { get; set; } = new();
}
