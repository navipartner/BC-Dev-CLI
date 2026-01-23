using System.Text.Json.Serialization;

namespace BCDev.Models;

/// <summary>
/// Represents the app.json manifest file for an AL application
/// </summary>
public class AppJson
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("brief")]
    public string? Brief { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("application")]
    public string? Application { get; set; }

    [JsonPropertyName("runtime")]
    public string? Runtime { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("idRanges")]
    public List<IdRange>? IdRanges { get; set; }

    [JsonPropertyName("dependencies")]
    public List<AppDependency>? Dependencies { get; set; }

    [JsonPropertyName("features")]
    public List<string>? Features { get; set; }

    /// <summary>
    /// Generate the output filename for the compiled app
    /// Format: {Publisher}_{Name}_{Version}.app
    /// </summary>
    public string GetAppFileName()
    {
        var safeName = Name.Replace(" ", "_");
        var safePublisher = Publisher.Replace(" ", "_");
        return $"{safePublisher}_{safeName}_{Version}.app";
    }
}

public class IdRange
{
    [JsonPropertyName("from")]
    public int From { get; set; }

    [JsonPropertyName("to")]
    public int To { get; set; }
}

public class AppDependency
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}
