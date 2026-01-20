using System.Text.Json.Serialization;

namespace BCDev.Models;

/// <summary>
/// Represents the launch.json configurations file from VS Code
/// </summary>
public class LaunchConfigurations
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("configurations")]
    public List<LaunchConfiguration> Configurations { get; set; } = new();
}

/// <summary>
/// Represents a single launch configuration entry
/// </summary>
public class LaunchConfiguration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "al";

    [JsonPropertyName("request")]
    public string Request { get; set; } = "launch";

    [JsonPropertyName("server")]
    public string Server { get; set; } = "http://localhost";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 7049;

    [JsonPropertyName("serverInstance")]
    public string ServerInstance { get; set; } = "BC";

    [JsonPropertyName("tenant")]
    public string? Tenant { get; set; } = "default";

    [JsonPropertyName("authentication")]
    public AuthenticationMethod Authentication { get; set; } = AuthenticationMethod.UserPassword;

    [JsonPropertyName("startupObjectId")]
    public int? StartupObjectId { get; set; }

    [JsonPropertyName("startupObjectType")]
    public StartupObjectType StartupObjectType { get; set; } = StartupObjectType.Page;

    [JsonPropertyName("startupCompany")]
    public string? StartupCompany { get; set; }

    [JsonPropertyName("schemaUpdateMode")]
    public SchemaUpdateMode SchemaUpdateMode { get; set; } = SchemaUpdateMode.Synchronize;

    [JsonPropertyName("breakOnError")]
    public BreakOnErrorOption BreakOnError { get; set; } = BreakOnErrorOption.None;

    [JsonPropertyName("launchBrowser")]
    public bool LaunchBrowser { get; set; } = true;

    [JsonPropertyName("enableSqlInformationDebugger")]
    public bool EnableSqlInformationDebugger { get; set; } = true;

    [JsonPropertyName("enableLongRunningSqlStatements")]
    public bool EnableLongRunningSqlStatements { get; set; } = true;

    [JsonPropertyName("longRunningSqlStatementsThreshold")]
    public int LongRunningSqlStatementsThreshold { get; set; } = 500;

    [JsonPropertyName("numberOfSqlStatements")]
    public int NumberOfSqlStatements { get; set; } = 10;

    /// <summary>
    /// Build the client services URL from configuration
    /// </summary>
    public string GetClientServicesUrl()
    {
        var baseUrl = Server.TrimEnd('/');
        var tenant = string.IsNullOrEmpty(Tenant) ? "default" : Tenant;
        return $"{baseUrl}:{Port}/{ServerInstance}?tenant={tenant}";
    }

    /// <summary>
    /// Build the development services URL from configuration (port 7049)
    /// </summary>
    public string GetDevServicesUrl()
    {
        var baseUrl = Server.TrimEnd('/');
        var port = Port; // Dev service typically on same port as client services
        return $"{baseUrl}:{port}/{ServerInstance}/dev/";
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthenticationMethod
{
    UserPassword,
    AAD,
    MicrosoftEntraID,
    Windows
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StartupObjectType
{
    Page,
    Table,
    Report,
    Query
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SchemaUpdateMode
{
    Synchronize,
    Recreate,
    ForceSync
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BreakOnErrorOption
{
    None,
    All,
    ExcludeTry
}
