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
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var configs = JsonSerializer.Deserialize<LaunchConfigurations>(json, options);
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
