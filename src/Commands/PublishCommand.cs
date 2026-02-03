using System.CommandLine;
using BCDev.Services;

namespace BCDev.Commands;

public static class PublishCommand
{
    public static Command Create()
    {
        var command = new Command("publish", "Publish an AL application to Business Central");

        // Path options
        var appPathOption = new Option<string>(
            name: "-appPath",
            description: "Path to .app file")
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

        // Auth options
        var usernameOption = new Option<string?>(
            name: "-Username",
            description: "Username for UserPassword authentication");

        var passwordOption = new Option<string?>(
            name: "-Password",
            description: "Password for UserPassword authentication");

        command.AddOption(appPathOption);
        command.AddOption(launchJsonPathOption);
        command.AddOption(launchJsonNameOption);
        command.AddOption(usernameOption);
        command.AddOption(passwordOption);

        command.SetHandler(async (context) =>
        {
            var appPath = context.ParseResult.GetValueForOption(appPathOption)!;
            var launchJsonPath = context.ParseResult.GetValueForOption(launchJsonPathOption)!;
            var launchJsonName = context.ParseResult.GetValueForOption(launchJsonNameOption)!;
            var username = context.ParseResult.GetValueForOption(usernameOption);
            var password = context.ParseResult.GetValueForOption(passwordOption);

            await ExecuteAsync(appPath, launchJsonPath, launchJsonName, username, password);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string appPath,
        string launchJsonPath,
        string launchJsonName,
        string? username,
        string? password)
    {
        var publishService = new PublishService();
        var result = await publishService.PublishAsync(
            appPath, launchJsonPath, launchJsonName, username, password);

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, JsonContext.Default.PublishResult));

        Environment.ExitCode = result.Success ? 0 : 1;
    }
}
