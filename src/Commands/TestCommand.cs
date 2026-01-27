using System.CommandLine;
using BCDev.BC;
using BCDev.Services;

namespace BCDev.Commands;

public static class TestCommand
{
    private const string DefaultBCVersion = "27.0";

    public static Command Create()
    {
        var command = new Command("test", "Run tests against Business Central");

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

        var usernameOption = new Option<string?>(
            name: "-Username",
            description: "Username for UserPassword authentication");

        var passwordOption = new Option<string?>(
            name: "-Password",
            description: "Password for UserPassword authentication");

        var codeunitIdOption = new Option<int?>(
            name: "-CodeunitId",
            description: "Specific test codeunit ID to run");

        var methodNameOption = new Option<string?>(
            name: "-MethodName",
            description: "Specific test method name to run");

        var testAllOption = new Option<bool>(
            name: "-all",
            description: "Run all available test codeunits",
            getDefaultValue: () => false);

        var testSuiteOption = new Option<string>(
            name: "-testSuite",
            description: "Test suite name (used internally)",
            getDefaultValue: () => "DEFAULT");

        var timeoutMinutesOption = new Option<int>(
            name: "-timeoutMinutes",
            description: "Timeout in minutes for test execution",
            getDefaultValue: () => 30);

        command.AddOption(launchJsonPathOption);
        command.AddOption(launchJsonNameOption);
        command.AddOption(usernameOption);
        command.AddOption(passwordOption);
        command.AddOption(codeunitIdOption);
        command.AddOption(methodNameOption);
        command.AddOption(testAllOption);
        command.AddOption(testSuiteOption);
        command.AddOption(timeoutMinutesOption);

        command.SetHandler(async (context) =>
        {
            var launchJsonPath = context.ParseResult.GetValueForOption(launchJsonPathOption)!;
            var launchJsonName = context.ParseResult.GetValueForOption(launchJsonNameOption)!;
            var username = context.ParseResult.GetValueForOption(usernameOption);
            var password = context.ParseResult.GetValueForOption(passwordOption);
            var codeunitId = context.ParseResult.GetValueForOption(codeunitIdOption);
            var methodName = context.ParseResult.GetValueForOption(methodNameOption);
            var testAll = context.ParseResult.GetValueForOption(testAllOption);
            var testSuite = context.ParseResult.GetValueForOption(testSuiteOption)!;
            var timeoutMinutes = context.ParseResult.GetValueForOption(timeoutMinutesOption);

            // Determine BC version from app.json and set it for BCClientLoader
            await SetBCVersionFromAppJsonAsync(launchJsonPath);

            await ExecuteAsync(launchJsonPath, launchJsonName, username, password,
                codeunitId, methodName, testAll, testSuite, timeoutMinutes);
        });

        return command;
    }

    /// <summary>
    /// Determines BC version from app.json and configures BCClientLoader.
    /// </summary>
    private static async Task SetBCVersionFromAppJsonAsync(string launchJsonPath)
    {
        var artifactService = new ArtifactService();
        string version;

        // Try to find app.json in the same folder as launch.json
        var launchDir = Path.GetDirectoryName(launchJsonPath);
        var appJsonPath = launchDir != null ? Path.Combine(launchDir, "..", "app.json") : null;

        if (appJsonPath != null && File.Exists(appJsonPath))
        {
            version = await artifactService.ResolveVersionFromAppJsonAsync(appJsonPath);
        }
        else
        {
            // Use default version if no app.json found
            version = DefaultBCVersion;
            Console.WriteLine($"No app.json found, using default BC version {version}");
        }

        // Set the version for BCClientLoader - it will download if needed
        BCClientLoader.Version = version;
    }

    private static async Task ExecuteAsync(
        string launchJsonPath,
        string launchJsonName,
        string? username,
        string? password,
        int? codeunitId,
        string? methodName,
        bool testAll,
        string testSuite,
        int timeoutMinutes)
    {
        var testService = new TestService();
        var result = await testService.RunTestsAsync(
            launchJsonPath, launchJsonName, username, password,
            codeunitId, methodName, testAll, testSuite, timeoutMinutes);

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, JsonContext.Default.TestRunResult));

        Environment.ExitCode = result.Success ? 0 : 1;
    }
}
