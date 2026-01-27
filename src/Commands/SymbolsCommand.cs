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
