using System.CommandLine;
using BCDev.Models;
using BCDev.Services;

namespace BCDev.Commands;

public static class PublishCommand
{
    public static Command Create()
    {
        var command = new Command("publish", "Publish an AL application to Business Central");

        // Option to trigger recompile before publish
        var recompileOption = new Option<bool>(
            name: "-recompile",
            description: "Compile the app before publishing",
            getDefaultValue: () => false);

        // Path options
        var appPathOption = new Option<string?>(
            name: "-appPath",
            description: "Path to .app file (required if not using -recompile)");

        var appJsonPathOption = new Option<string?>(
            name: "-appJsonPath",
            description: "Path to app.json file (required with -recompile)");

        var compilerPathOption = new Option<string?>(
            name: "-compilerPath",
            description: "Path to alc.exe compiler (required with -recompile)");

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
        var authTypeOption = new Option<AuthType?>(
            name: "-authType",
            description: "Authentication type (UserPassword, AAD)");

        var usernameOption = new Option<string?>(
            name: "-Username",
            description: "Username for UserPassword authentication");

        var passwordOption = new Option<string?>(
            name: "-Password",
            description: "Password for UserPassword authentication");

        var bcClientDllPathOption = new Option<string?>(
            name: "-bcClientDllPath",
            description: "Path to BC client DLL (uses bundled version if not specified)");

        command.AddOption(recompileOption);
        command.AddOption(appPathOption);
        command.AddOption(appJsonPathOption);
        command.AddOption(compilerPathOption);
        command.AddOption(launchJsonPathOption);
        command.AddOption(launchJsonNameOption);
        command.AddOption(authTypeOption);
        command.AddOption(usernameOption);
        command.AddOption(passwordOption);
        command.AddOption(bcClientDllPathOption);

        command.SetHandler(async (context) =>
        {
            var recompile = context.ParseResult.GetValueForOption(recompileOption);
            var appPath = context.ParseResult.GetValueForOption(appPathOption);
            var appJsonPath = context.ParseResult.GetValueForOption(appJsonPathOption);
            var compilerPath = context.ParseResult.GetValueForOption(compilerPathOption);
            var launchJsonPath = context.ParseResult.GetValueForOption(launchJsonPathOption)!;
            var launchJsonName = context.ParseResult.GetValueForOption(launchJsonNameOption)!;
            var authType = context.ParseResult.GetValueForOption(authTypeOption);
            var username = context.ParseResult.GetValueForOption(usernameOption);
            var password = context.ParseResult.GetValueForOption(passwordOption);
            var bcClientDllPath = context.ParseResult.GetValueForOption(bcClientDllPathOption);

            await ExecuteAsync(recompile, appPath, appJsonPath, compilerPath,
                launchJsonPath, launchJsonName, authType, username, password, bcClientDllPath);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        bool recompile,
        string? appPath,
        string? appJsonPath,
        string? compilerPath,
        string launchJsonPath,
        string launchJsonName,
        AuthType? authType,
        string? username,
        string? password,
        string? bcClientDllPath)
    {
        // Validate options
        if (recompile)
        {
            if (string.IsNullOrEmpty(appJsonPath))
            {
                WriteError("Error: -appJsonPath is required when using -recompile");
                Environment.ExitCode = 1;
                return;
            }
            if (string.IsNullOrEmpty(compilerPath))
            {
                WriteError("Error: -compilerPath is required when using -recompile");
                Environment.ExitCode = 1;
                return;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(appPath))
            {
                WriteError("Error: -appPath is required when not using -recompile");
                Environment.ExitCode = 1;
                return;
            }
        }

        var publishService = new PublishService();
        var result = await publishService.PublishAsync(
            recompile, appPath, appJsonPath, compilerPath,
            launchJsonPath, launchJsonName, authType, username, password, bcClientDllPath);

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        }));

        Environment.ExitCode = result.Success ? 0 : 1;
    }

    private static void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }
}
