using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using BCDev.Models;

namespace BCDev.Services;

/// <summary>
/// Service for managing BC artifact downloads and caching.
/// Uses HTTP Range requests for efficient partial ZIP downloads.
/// </summary>
public class ArtifactService
{
    // New CDN URL (bcartifacts.azureedge.net is defunct)
    private const string ArtifactCdnBaseUrl = "https://bcartifacts-exdbf9fwegejdqak.b02.azurefd.net";
    private const string DefaultArtifactType = "sandbox";
    private const string DefaultCountry = "w1";
    
    private readonly HttpClient _httpClient = new();
    private List<VersionInfo>? _versionCache;

    /// <summary>
    /// Gets the OS-specific cache directory path
    /// </summary>
    public string GetCachePath()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "bcdev", "cache");
        }
        else
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".bcdev", "cache");
        }
    }

    /// <summary>
    /// Extracts major.minor version from a full platform version string
    /// </summary>
    public string ExtractMajorMinorVersion(string platformVersion)
    {
        if (string.IsNullOrWhiteSpace(platformVersion))
        {
            throw new ArgumentException("Platform version cannot be empty", nameof(platformVersion));
        }

        var parts = platformVersion.Split('.');
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid platform version format: {platformVersion}", nameof(platformVersion));
        }

        return $"{parts[0]}.{parts[1]}";
    }

    /// <summary>
    /// Gets the cache path for a specific version
    /// </summary>
    public string GetVersionCachePath(string version)
    {
        return Path.Combine(GetCachePath(), version);
    }

    /// <summary>
    /// Checks if artifacts for a version are already cached
    /// </summary>
    public bool IsVersionCached(string version)
    {
        var versionPath = GetVersionCachePath(version);
        if (!Directory.Exists(versionPath))
        {
            return false;
        }

        // Check for required files
        var clientDll = Path.Combine(versionPath, "Microsoft.Dynamics.Framework.UI.Client.dll");
        return File.Exists(clientDll);
    }

    /// <summary>
    /// Gets the compiler path for a cached version
    /// </summary>
    public string? GetCachedCompilerPath(string version)
    {
        var versionPath = GetVersionCachePath(version);
        
        // Try different possible names
        var alcExe = Path.Combine(versionPath, "alc.exe");
        if (File.Exists(alcExe)) return alcExe;
        
        var alc = Path.Combine(versionPath, "alc");
        if (File.Exists(alc)) return alc;
        
        return null;
    }

    /// <summary>
    /// Gets the client DLL path for a cached version
    /// </summary>
    public string? GetCachedClientDllPath(string version)
    {
        var versionPath = GetVersionCachePath(version);
        var dllPath = Path.Combine(versionPath, "Microsoft.Dynamics.Framework.UI.Client.dll");
        return File.Exists(dllPath) ? dllPath : null;
    }

    /// <summary>
    /// Resolves the BC version from an app.json file
    /// </summary>
    public async Task<string> ResolveVersionFromAppJsonAsync(string appJsonPath)
    {
        if (!File.Exists(appJsonPath))
        {
            throw new FileNotFoundException($"app.json not found: {appJsonPath}");
        }

        var json = await File.ReadAllTextAsync(appJsonPath);
        var appJson = JsonSerializer.Deserialize(json, JsonContext.Default.AppJson)
            ?? throw new InvalidOperationException("Failed to parse app.json");

        if (string.IsNullOrEmpty(appJson.Platform))
        {
            throw new InvalidOperationException("app.json does not contain a 'platform' field");
        }

        return ExtractMajorMinorVersion(appJson.Platform);
    }

    /// <summary>
    /// Fetches available versions from Microsoft's artifact index
    /// </summary>
    public async Task<List<VersionInfo>> FetchAvailableVersionsAsync(string artifactType = DefaultArtifactType)
    {
        if (_versionCache != null)
        {
            return _versionCache;
        }

        var indexUrl = $"{ArtifactCdnBaseUrl}/{artifactType}/indexes/platform.json";
        Console.WriteLine($"Fetching version index from Microsoft...");

        try
        {
            var response = await _httpClient.GetStringAsync(indexUrl);
            _versionCache = JsonSerializer.Deserialize(response, JsonContext.Default.ListVersionInfo)
                ?? new List<VersionInfo>();
            
            return _versionCache;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to fetch version index: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Finds the best matching full version for a major.minor version request
    /// </summary>
    public async Task<string?> FindBestVersionAsync(string majorMinorVersion, string artifactType = DefaultArtifactType)
    {
        var versions = await FetchAvailableVersionsAsync(artifactType);

        // Filter to matching major.minor versions
        var matching = versions
            .Where(v => v.Version.StartsWith(majorMinorVersion + "."))
            .OrderByDescending(v => v.CreationTime)
            .ToList();

        if (matching.Any())
        {
            return matching.First().Version;
        }

        return null;
    }

    /// <summary>
    /// Gets the artifact download URL for a full version
    /// </summary>
    public string GetArtifactUrl(string fullVersion, string artifactType = DefaultArtifactType, string country = DefaultCountry)
    {
        // For the client DLL, we need the platform artifact
        // For AL compiler, it's also in the platform artifact
        return $"{ArtifactCdnBaseUrl}/{artifactType}/{fullVersion}/platform";
    }

    /// <summary>
    /// Ensures artifacts are available for the specified version, downloading if necessary
    /// </summary>
    public async Task<string> EnsureArtifactsAsync(string majorMinorVersion, string artifactType = DefaultArtifactType)
    {
        // Use major.minor as cache key for simplicity
        var versionPath = GetVersionCachePath(majorMinorVersion);

        if (IsVersionCached(majorMinorVersion))
        {
            Console.WriteLine($"Using cached BC {majorMinorVersion} artifacts");
            return versionPath;
        }

        Console.WriteLine($"Resolving BC {majorMinorVersion} version...");

        // Find the best matching full version
        var fullVersion = await FindBestVersionAsync(majorMinorVersion, artifactType)
            ?? throw new InvalidOperationException($"No BC artifacts found for version {majorMinorVersion}");

        Console.WriteLine($"Found version {fullVersion}");
        Console.WriteLine($"Downloading BC {majorMinorVersion} artifacts from Microsoft...");

        // Create cache directory
        Directory.CreateDirectory(versionPath);

        try
        {
            var artifactUrl = GetArtifactUrl(fullVersion, artifactType);
            await DownloadAndExtractArtifactsAsync(artifactUrl, versionPath);
            Console.WriteLine($"Cached to {versionPath}");
            return versionPath;
        }
        catch
        {
            // Clean up on failure
            if (Directory.Exists(versionPath))
            {
                Directory.Delete(versionPath, true);
            }
            throw;
        }
    }

    private async Task DownloadAndExtractArtifactsAsync(string artifactUrl, string targetPath)
    {
        Console.WriteLine("Using HTTP Range requests for efficient partial download...");
        await DownloadWithRangeRequestsAsync(artifactUrl, targetPath);
    }

    private async Task DownloadWithRangeRequestsAsync(string artifactUrl, string targetPath)
    {
        // Step 1: Get file size and verify Range support
        var headRequest = new HttpRequestMessage(HttpMethod.Head, artifactUrl);
        var headResponse = await _httpClient.SendAsync(headRequest);
        headResponse.EnsureSuccessStatusCode();

        if (headResponse.Headers.AcceptRanges.All(r => r != "bytes"))
        {
            throw new NotSupportedException("Server does not support Range requests");
        }

        var totalSize = headResponse.Content.Headers.ContentLength 
            ?? throw new InvalidOperationException("Content-Length header missing");
        
        Console.WriteLine($"Archive size: {totalSize / 1024 / 1024:N0} MB");

        // Step 2: Download ZIP End of Central Directory (last 64KB)
        var eocdSize = 65536;
        var eocdStart = totalSize - eocdSize;
        var eocdData = await DownloadRangeAsync(artifactUrl, eocdStart, totalSize - 1);

        // Step 3: Parse EOCD to find Central Directory
        var (cdOffset, cdSize, entryCount) = ParseEndOfCentralDirectory(eocdData);
        Console.WriteLine($"Found {entryCount:N0} files in archive");

        // Step 4: Download Central Directory
        var cdData = await DownloadRangeAsync(artifactUrl, cdOffset, cdOffset + cdSize - 1);
        Console.WriteLine($"Downloaded file index ({cdSize / 1024:N0} KB)");

        // Step 5: Parse Central Directory to find target files
        var targetFiles = new[]
        {
            "Microsoft.Dynamics.Framework.UI.Client.dll",
            "Microsoft.Dynamics.Framework.UI.Client.Interactions.dll",
            "ALLanguage.vsix"
        };

        var entries = ParseCentralDirectory(cdData, targetFiles);
        
        // Step 6: Download and extract each target file
        var extractedFiles = new List<string>();

        foreach (var entry in entries)
        {
            Console.WriteLine($"Downloading {Path.GetFileName(entry.Name)} ({entry.CompressedSize / 1024:N0} KB)...");
            
            // Download local file header + compressed data
            var headerBuffer = 200; // Account for variable length fields
            var rangeEnd = entry.LocalHeaderOffset + headerBuffer + entry.CompressedSize;
            var fileData = await DownloadRangeAsync(artifactUrl, entry.LocalHeaderOffset, rangeEnd);

            // Parse local file header and extract
            await ExtractZipEntry(fileData, entry, targetPath);
            extractedFiles.Add(Path.GetFileName(entry.Name));
        }

        // Step 7: If we got the VSIX, extract compiler from within it
        var vsixPath = Path.Combine(targetPath, "ALLanguage.vsix");
        if (File.Exists(vsixPath))
        {
            Console.WriteLine("Extracting compiler from AL Language extension...");
            ExtractCompilerFromVsix(vsixPath, targetPath);
            File.Delete(vsixPath); // Clean up temporary VSIX
        }

        // Make compiler executable on non-Windows
        if (!OperatingSystem.IsWindows())
        {
            var alcPath = Path.Combine(targetPath, "alc");
            if (File.Exists(alcPath))
            {
                File.SetUnixFileMode(alcPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        Console.WriteLine($"Extracted: {string.Join(", ", extractedFiles.Where(f => !f.EndsWith(".vsix")))}");
    }

    private async Task<byte[]> DownloadRangeAsync(string url, long start, long end)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(start, end);
        
        var response = await _httpClient.SendAsync(request);
        
        if (response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            throw new InvalidOperationException($"Expected 206 Partial Content, got {response.StatusCode}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    private (long cdOffset, long cdSize, int entryCount) ParseEndOfCentralDirectory(byte[] data)
    {
        // Find EOCD signature (0x06054b50 = PK\x05\x06)
        var eocdSig = new byte[] { 0x50, 0x4B, 0x05, 0x06 };
        var eocdPos = -1;
        
        for (var i = data.Length - 22; i >= 0; i--)
        {
            if (data[i] == eocdSig[0] && data[i + 1] == eocdSig[1] && 
                data[i + 2] == eocdSig[2] && data[i + 3] == eocdSig[3])
            {
                eocdPos = i;
                break;
            }
        }

        if (eocdPos < 0)
        {
            throw new InvalidOperationException("Could not find ZIP End of Central Directory");
        }

        // Parse EOCD (22 bytes minimum)
        using var ms = new MemoryStream(data, eocdPos, data.Length - eocdPos);
        using var reader = new BinaryReader(ms);

        reader.ReadUInt32(); // Signature
        reader.ReadUInt16(); // Disk number
        reader.ReadUInt16(); // Disk with CD
        reader.ReadUInt16(); // Entries on disk
        var entryCount = reader.ReadUInt16(); // Total entries
        var cdSize = reader.ReadUInt32(); // CD size
        var cdOffset = reader.ReadUInt32(); // CD offset

        // Check for ZIP64 (values = 0xFFFFFFFF)
        if (cdOffset == 0xFFFFFFFF)
        {
            throw new NotSupportedException("ZIP64 format not currently supported in partial downloads");
        }

        return (cdOffset, cdSize, entryCount);
    }

    private List<ZipCentralDirectoryEntry> ParseCentralDirectory(byte[] data, string[] targetFileNames)
    {
        // Collect all matching entries, then select best ones
        var allMatches = new Dictionary<string, List<ZipCentralDirectoryEntry>>();
        var cdSig = new byte[] { 0x50, 0x4B, 0x01, 0x02 };
        var pos = 0;

        while (pos < data.Length - 46)
        {
            if (data[pos] != cdSig[0] || data[pos + 1] != cdSig[1] ||
                data[pos + 2] != cdSig[2] || data[pos + 3] != cdSig[3])
            {
                break;
            }

            using var ms = new MemoryStream(data, pos, data.Length - pos);
            using var reader = new BinaryReader(ms);

            reader.ReadUInt32(); // Signature
            reader.ReadUInt16(); // Version made by
            reader.ReadUInt16(); // Version needed
            reader.ReadUInt16(); // Flags
            var compression = reader.ReadUInt16();
            reader.ReadUInt16(); // Mod time
            reader.ReadUInt16(); // Mod date
            reader.ReadUInt32(); // CRC32
            var compressedSize = reader.ReadUInt32();
            var uncompressedSize = reader.ReadUInt32();
            var nameLen = reader.ReadUInt16();
            var extraLen = reader.ReadUInt16();
            var commentLen = reader.ReadUInt16();
            reader.ReadUInt16(); // Disk start
            reader.ReadUInt16(); // Internal attrs
            reader.ReadUInt32(); // External attrs
            var localHeaderOffset = reader.ReadUInt32();

            var nameBytes = reader.ReadBytes(nameLen);
            var name = System.Text.Encoding.UTF8.GetString(nameBytes);

            // Check if this is a target file
            var fileName = Path.GetFileName(name);
            var matchedTarget = targetFileNames.FirstOrDefault(t => 
                fileName.Equals(t, StringComparison.OrdinalIgnoreCase));
            
            if (matchedTarget != null)
            {
                if (!allMatches.ContainsKey(matchedTarget))
                {
                    allMatches[matchedTarget] = new List<ZipCentralDirectoryEntry>();
                }
                
                allMatches[matchedTarget].Add(new ZipCentralDirectoryEntry
                {
                    Name = name,
                    Compression = compression,
                    CompressedSize = compressedSize,
                    UncompressedSize = uncompressedSize,
                    LocalHeaderOffset = localHeaderOffset
                });
            }

            pos += 46 + nameLen + extraLen + commentLen;
        }

        // Select best entry for each target file
        var result = new List<ZipCentralDirectoryEntry>();
        foreach (var kvp in allMatches)
        {
            var candidates = kvp.Value;
            if (candidates.Count == 1)
            {
                result.Add(candidates[0]);
            }
            else
            {
                // Multiple matches - prefer certain paths based on file type
                var selected = SelectBestEntry(kvp.Key, candidates);
                result.Add(selected);
            }
        }

        return result;
    }

    private ZipCentralDirectoryEntry SelectBestEntry(string targetFile, List<ZipCentralDirectoryEntry> candidates)
    {
        // Preference rules for when multiple files match:
        // - For client DLLs: prefer "Test Assemblies/" path (standalone, fewer dependencies)
        // - For VSIX: prefer "al development environment/" path
        // - Otherwise: prefer shorter paths (usually less nested = more canonical)
        
        var targetLower = targetFile.ToLowerInvariant();
        
        if (targetLower.Contains("client"))
        {
            // Client DLL - prefer Test Assemblies path
            var testAssemblies = candidates.FirstOrDefault(c => 
                c.Name.Contains("Test Assemblies", StringComparison.OrdinalIgnoreCase));
            if (testAssemblies != null) return testAssemblies;
        }
        else if (targetLower.EndsWith(".vsix"))
        {
            // VSIX - prefer al development environment path
            var alDev = candidates.FirstOrDefault(c => 
                c.Name.Contains("al development environment", StringComparison.OrdinalIgnoreCase));
            if (alDev != null) return alDev;
        }

        // Fallback: shortest path (usually most canonical location)
        return candidates.OrderBy(c => c.Name.Length).First();
    }

    private Task ExtractZipEntry(byte[] data, ZipCentralDirectoryEntry entry, string targetPath)
    {
        // Parse local file header
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        var sig = reader.ReadUInt32();
        if (sig != 0x04034B50)
        {
            throw new InvalidOperationException($"Invalid local file header signature: {sig:X8}");
        }

        reader.ReadUInt16(); // Version
        reader.ReadUInt16(); // Flags
        var compression = reader.ReadUInt16();
        reader.ReadUInt16(); // Mod time
        reader.ReadUInt16(); // Mod date
        reader.ReadUInt32(); // CRC32
        reader.ReadUInt32(); // Compressed size
        reader.ReadUInt32(); // Uncompressed size
        var nameLen = reader.ReadUInt16();
        var extraLen = reader.ReadUInt16();

        reader.ReadBytes(nameLen); // Skip name
        reader.ReadBytes(extraLen); // Skip extra

        // Read compressed data
        var compressedData = reader.ReadBytes((int)entry.CompressedSize);

        // Decompress if needed
        byte[] outputData;
        if (compression == 0)
        {
            outputData = compressedData;
        }
        else if (compression == 8) // Deflate
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            deflateStream.CopyTo(outputStream);
            outputData = outputStream.ToArray();
        }
        else
        {
            throw new NotSupportedException($"Compression method {compression} not supported");
        }

        // Save the file
        var outputPath = Path.Combine(targetPath, Path.GetFileName(entry.Name));
        File.WriteAllBytes(outputPath, outputData);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the platform-specific directory name used in VSIX structure
    /// </summary>
    private static string GetPlatformDirectory()
    {
        if (OperatingSystem.IsWindows())
            return "win32";
        if (OperatingSystem.IsMacOS())
            return "darwin";
        if (OperatingSystem.IsLinux())
            return "linux";

        throw new PlatformNotSupportedException(
            $"Unsupported platform: {Environment.OSVersion.Platform}. BC compiler is only available for Windows, macOS, and Linux.");
    }

    private void ExtractCompilerFromVsix(string vsixPath, string targetPath)
    {
        // VSIX is a ZIP file containing the AL compiler
        // Structure: extension/bin/{platform}/ contains alc + all .NET runtime dependencies
        // We must extract the ENTIRE platform folder for the compiler to work
        using var archive = ZipFile.OpenRead(vsixPath);

        var platform = GetPlatformDirectory();
        var platformPath = $"extension/bin/{platform}/";
        Console.WriteLine($"  Extracting {platform} compiler and runtime...");

        var extractCount = 0;
        var alcFound = false;

        foreach (var entry in archive.Entries)
        {
            // Check if this entry is in the platform-specific bin directory
            if (!entry.FullName.StartsWith(platformPath, StringComparison.OrdinalIgnoreCase))
                continue;

            // Get the relative path within the platform folder
            var relativePath = entry.FullName.Substring(platformPath.Length);

            // Skip if it's just the directory entry
            if (string.IsNullOrEmpty(relativePath))
                continue;

            var outputPath = Path.Combine(targetPath, relativePath);

            // Create subdirectories if needed
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Skip directory entries (they end with /)
            if (entry.FullName.EndsWith("/"))
                continue;

            entry.ExtractToFile(outputPath, overwrite: true);
            extractCount++;

            // Log the main compiler executable
            var fileName = Path.GetFileName(entry.FullName);
            if (fileName.Equals("alc", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("alc.exe", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  Extracted: {fileName} ({platform})");
                alcFound = true;
            }
        }

        Console.WriteLine($"  Extracted {extractCount} files");

        if (!alcFound)
        {
            throw new InvalidOperationException(
                $"AL compiler not found in VSIX at expected path 'extension/bin/{platform}/'. " +
                $"Extracted {extractCount} files but none was alc or alc.exe.");
        }
    }

    private class ZipCentralDirectoryEntry
    {
        public string Name { get; set; } = string.Empty;
        public ushort Compression { get; set; }
        public uint CompressedSize { get; set; }
        public uint UncompressedSize { get; set; }
        public uint LocalHeaderOffset { get; set; }
    }

    /// <summary>
    /// Detects the actual BC version running on a server by querying the dev/packages endpoint.
    /// Returns major.minor format (e.g., "27.3").
    /// </summary>
    public async Task<string?> DetectServerVersionAsync(string serverUrl, System.Net.ICredentials credentials)
    {
        try
        {
            // Parse service URL to extract base URL and tenant
            // Input format: https://host:port/instance?tenant=xxx
            var uri = new Uri(serverUrl);
            var host = uri.GetLeftPart(UriPartial.Authority); // https://host:port
            var instance = uri.AbsolutePath.TrimStart('/').Split('/')[0]; // instance name

            // Get tenant from query string, default to "default"
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var tenant = query["tenant"] ?? "default";

            var packagesUrl = $"{host}/{instance}/dev/packages?publisher=Microsoft&appName=Application&versionText=1.0.0.0&tenant={tenant}";

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            using var client = new HttpClient(handler);

            // Add explicit Basic auth header if we have NetworkCredential
            if (credentials is System.Net.NetworkCredential netCred)
            {
                var authString = $"{netCred.UserName}:{netCred.Password}";
                var authBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authString));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authBase64);
            }

            // Use Range request to minimize download
            using var request = new HttpRequestMessage(HttpMethod.Get, packagesUrl);
            request.Headers.Range = new RangeHeaderValue(0, 0);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // Parse Content-Disposition header for filename
            // Format: attachment; filename=Microsoft_Application_27.3.44313.44821.app
            var contentDisposition = response.Content.Headers.ContentDisposition;
            var filename = contentDisposition?.FileName?.Trim('"')
                ?? contentDisposition?.FileNameStar?.Trim('"');

            if (string.IsNullOrEmpty(filename))
            {
                return null;
            }

            // Extract version from filename: Microsoft_Application_27.3.44313.44821.app
            // We want "27.3"
            var parts = filename.Split('_');
            if (parts.Length >= 3)
            {
                var versionPart = parts[2]; // "27.3.44313.44821.app"
                var versionSegments = versionPart.Split('.');
                if (versionSegments.Length >= 2)
                {
                    return $"{versionSegments[0]}.{versionSegments[1]}";
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Version info from Microsoft's artifact index.
/// Note: Explicit JsonPropertyName attributes required because Microsoft's CDN
/// uses PascalCase, but our JsonContext uses camelCase naming policy.
/// </summary>
public class VersionInfo
{
    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("CreationTime")]
    public DateTime CreationTime { get; set; }
}
