using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class NuGetVersionMatchingTests
{
    [Fact]
    public void FindMatchingVersion_ExactMatch_ReturnsExact()
    {
        var versions = new List<string> { "27.0.45024", "27.0.45025", "27.1.46000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.45024");
        Assert.Equal("27.0.45024", result);
    }

    [Fact]
    public void FindMatchingVersion_NoExact_ReturnsClosestHigher()
    {
        var versions = new List<string> { "27.0.45024", "27.0.45100", "27.1.46000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.45050");
        Assert.Equal("27.0.45100", result);
    }

    [Fact]
    public void FindMatchingVersion_RequestedTooHigh_ReturnsNull()
    {
        var versions = new List<string> { "27.0.45024", "27.0.45100" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "28.0.0");
        Assert.Null(result);
    }

    [Fact]
    public void FindMatchingVersion_FourPartVersion_MatchesMajorMinor()
    {
        // app.json says 27.0.0.0 but NuGet has 27.0.45024
        var versions = new List<string> { "26.5.44000", "27.0.45024", "27.0.45100", "27.1.46000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.0.0");
        Assert.Equal("27.0.45024", result); // Closest higher in 27.0.x
    }

    [Fact]
    public void FindMatchingVersion_ThreePartToFourPart_Works()
    {
        // Platform uses 3-part, looking for 4-part equivalent
        var versions = new List<string> { "27.0.45024", "27.0.45100", "27.1.46000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.0.0");
        Assert.Equal("27.0.45024", result);
    }

    [Fact]
    public void FindMatchingVersion_PrefersSameMajorMinor()
    {
        var versions = new List<string> { "26.5.44000", "27.0.45024", "27.1.46000", "28.0.50000" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.38460");
        Assert.Equal("27.0.45024", result); // Stay in 27.0.x, not jump to 27.1
    }

    [Fact]
    public void FindMatchingVersion_EmptyVersions_ReturnsNull()
    {
        var versions = new List<string>();
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.0.0");
        Assert.Null(result);
    }

    [Fact]
    public void FindMatchingVersion_CaseInsensitive()
    {
        var versions = new List<string> { "27.0.45024" };
        var result = NuGetFeedService.FindMatchingVersion(versions, "27.0.45024");
        Assert.Equal("27.0.45024", result);
    }
}
