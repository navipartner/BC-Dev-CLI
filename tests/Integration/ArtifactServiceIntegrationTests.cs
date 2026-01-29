using System.Net.Http.Headers;
using BCDev.Services;
using Xunit;
using Xunit.Abstractions;

namespace BCDev.Tests;

/// <summary>
/// Integration tests for ArtifactService HTTP Range downloads.
/// These tests require network access and hit real Microsoft CDN.
/// </summary>
public class ArtifactServiceIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient = new();
    private readonly ArtifactService _artifactService = new();

    // Use major.minor version - the actual URL is resolved dynamically
    private const string TestVersion = "27.0";

    public ArtifactServiceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private async Task<string> GetCurrentArtifactUrlAsync()
    {
        var fullVersion = await _artifactService.FindBestVersionAsync(TestVersion)
            ?? throw new InvalidOperationException($"No artifacts found for BC {TestVersion}");
        _output.WriteLine($"Resolved version {TestVersion} to {fullVersion}");
        return _artifactService.GetArtifactUrl(fullVersion);
    }

    [Fact]
    public async Task ServerSupportsRangeRequests()
    {
        var artifactUrl = await GetCurrentArtifactUrlAsync();
        _output.WriteLine($"Testing URL: {artifactUrl}");

        // HEAD request to check Range support
        var headRequest = new HttpRequestMessage(HttpMethod.Head, artifactUrl);
        var response = await _httpClient.SendAsync(headRequest);

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Content-Length: {response.Content.Headers.ContentLength:N0} bytes");
        _output.WriteLine($"Accept-Ranges: {string.Join(", ", response.Headers.AcceptRanges)}");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("bytes", response.Headers.AcceptRanges);
        Assert.True(response.Content.Headers.ContentLength > 1_000_000_000, "Archive should be > 1GB");
    }

    [Fact]
    public async Task CanDownloadPartialContent()
    {
        var artifactUrl = await GetCurrentArtifactUrlAsync();

        // First get total size
        var headRequest = new HttpRequestMessage(HttpMethod.Head, artifactUrl);
        var headResponse = await _httpClient.SendAsync(headRequest);
        var totalSize = headResponse.Content.Headers.ContentLength ?? 0;

        // Download last 64KB using Range
        var rangeStart = totalSize - 65536;
        var rangeRequest = new HttpRequestMessage(HttpMethod.Get, artifactUrl);
        rangeRequest.Headers.Range = new RangeHeaderValue(rangeStart, totalSize - 1);

        var response = await _httpClient.SendAsync(rangeRequest);
        
        _output.WriteLine($"Range request status: {response.StatusCode}");
        
        Assert.Equal(System.Net.HttpStatusCode.PartialContent, response.StatusCode);

        var data = await response.Content.ReadAsByteArrayAsync();
        _output.WriteLine($"Downloaded: {data.Length:N0} bytes");
        
        Assert.Equal(65536, data.Length);
    }

    [Fact]
    public async Task CanParseZipEndOfCentralDirectory()
    {
        var artifactUrl = await GetCurrentArtifactUrlAsync();

        // Get total size
        var headRequest = new HttpRequestMessage(HttpMethod.Head, artifactUrl);
        var headResponse = await _httpClient.SendAsync(headRequest);
        var totalSize = headResponse.Content.Headers.ContentLength ?? 0;

        // Download EOCD
        var rangeStart = totalSize - 65536;
        var rangeRequest = new HttpRequestMessage(HttpMethod.Get, artifactUrl);
        rangeRequest.Headers.Range = new RangeHeaderValue(rangeStart, totalSize - 1);
        var response = await _httpClient.SendAsync(rangeRequest);
        var data = await response.Content.ReadAsByteArrayAsync();

        // Find EOCD signature (PK\x05\x06)
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

        Assert.True(eocdPos >= 0, "Should find EOCD signature");
        _output.WriteLine($"EOCD found at offset {eocdPos}");

        // Parse EOCD
        using var ms = new MemoryStream(data, eocdPos, data.Length - eocdPos);
        using var reader = new BinaryReader(ms);
        reader.ReadUInt32(); // Signature
        reader.ReadUInt16(); // Disk number
        reader.ReadUInt16(); // Disk with CD
        reader.ReadUInt16(); // Entries on disk
        var entryCount = reader.ReadUInt16();
        var cdSize = reader.ReadUInt32();
        var cdOffset = reader.ReadUInt32();

        _output.WriteLine($"Files in archive: {entryCount:N0}");
        _output.WriteLine($"Central Directory size: {cdSize:N0} bytes");
        _output.WriteLine($"Central Directory offset: {cdOffset:N0}");

        Assert.True(entryCount > 1000, "Should have many files");
        Assert.True(cdSize > 100_000, "CD should be > 100KB");
    }
}
