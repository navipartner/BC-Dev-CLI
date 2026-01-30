using System.CommandLine;
using System.Text.Json;
using BCDev.Services;

namespace BCDev.Commands;

public static class SymbolsCommand
{
    public static Command Create()
    {
        var command = new Command("symbols", "Download symbol packages from NuGet feeds or Business Central server");

        var appJsonPathOption = new Option<string>(
            name: "-appJsonPath",
            description: "Path to app.json file")
        {
            IsRequired = true
        };

        var packageCachePathOption = new Option<string?>(
            name: "-packageCachePath",
            description: "Path to output folder (defaults to .alpackages next to app.json)");

        var countryOption = new Option<string>(
            name: "-country",
            description: "Country/region code for localized symbols (e.g., us, de, dk). Default 'w1' uses country-less packages.",
            getDefaultValue: () => "w1");

        var fromServerOption = new Option<bool>(
            name: "-fromServer",
            description: "Download from BC server instead of NuGet feeds (requires launch.json)",
            getDefaultValue: () => false);

        var launchJsonPathOption = new Option<string?>(
            name: "-launchJsonPath",
            description: "Path to launch.json file (required for server mode)");

        var launchJsonNameOption = new Option<string?>(
            name: "-launchJsonName",
            description: "Configuration name in launch.json (required for server mode)");

        var usernameOption = new Option<string?>(
            name: "-Username",
            description: "Username for authentication (server mode)");

        var passwordOption = new Option<string?>(
            name: "-Password",
            description: "Password for authentication (server mode)");

        command.AddOption(appJsonPathOption);
        command.AddOption(packageCachePathOption);
        command.AddOption(countryOption);
        command.AddOption(fromServerOption);
        command.AddOption(launchJsonPathOption);
        command.AddOption(launchJsonNameOption);
        command.AddOption(usernameOption);
        command.AddOption(passwordOption);

        command.SetHandler(async (context) =>
        {
            var appJsonPath = context.ParseResult.GetValueForOption(appJsonPathOption)!;
            var packageCachePath = context.ParseResult.GetValueForOption(packageCachePathOption);
            var country = context.ParseResult.GetValueForOption(countryOption)!;
            var fromServer = context.ParseResult.GetValueForOption(fromServerOption);
            var launchJsonPath = context.ParseResult.GetValueForOption(launchJsonPathOption);
            var launchJsonName = context.ParseResult.GetValueForOption(launchJsonNameOption);
            var username = context.ParseResult.GetValueForOption(usernameOption);
            var password = context.ParseResult.GetValueForOption(passwordOption);

            await ExecuteAsync(appJsonPath, packageCachePath, country, fromServer,
                launchJsonPath, launchJsonName, username, password);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string appJsonPath,
        string? packageCachePath,
        string country,
        bool fromServer,
        string? launchJsonPath,
        string? launchJsonName,
        string? username,
        string? password)
    {
        var symbolService = new SymbolService();

        Models.SymbolsResult result;

        if (fromServer)
        {
            // Server mode (opt-in) - validate required options
            if (string.IsNullOrEmpty(launchJsonPath))
            {
                Console.Error.WriteLine("Error: -launchJsonPath is required when using -fromServer");
                Environment.ExitCode = 1;
                return;
            }
            if (string.IsNullOrEmpty(launchJsonName))
            {
                Console.Error.WriteLine("Error: -launchJsonName is required when using -fromServer");
                Environment.ExitCode = 1;
                return;
            }

            result = await symbolService.DownloadFromServerAsync(
                appJsonPath, launchJsonPath, launchJsonName, packageCachePath, username, password);
        }
        else
        {
            // NuGet mode (default)
            result = await symbolService.DownloadFromNuGetAsync(
                appJsonPath, packageCachePath, country);
        }

        Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.SymbolsResult));

        Environment.ExitCode = result.Success ? 0 : 1;
    }
}
