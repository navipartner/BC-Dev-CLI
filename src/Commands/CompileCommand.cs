using System.CommandLine;
using BCDev.Services;

namespace BCDev.Commands;

public static class CompileCommand
{
    public static Command Create()
    {
        var command = new Command("compile", "Compile an AL application using alc.exe");

        var appJsonPathOption = new Option<string>(
            name: "-appJsonPath",
            description: "Path to app.json file")
        {
            IsRequired = true
        };

        var packageCachePathOption = new Option<string?>(
            name: "-packageCachePath",
            description: "Path to .alpackages folder containing symbol packages (defaults to .alpackages in app folder)");

        var suppressWarningsOption = new Option<bool>(
            name: "-suppressWarnings",
            description: "Suppress compiler warnings from the output",
            getDefaultValue: () => false);

        var generateReportLayoutOption = new Option<bool>(
            name: "-generateReportLayout",
            description: "Generate report layout files during compilation",
            getDefaultValue: () => false);

        var parallelOption = new Option<bool>(
            name: "-parallel",
            description: "Enable parallel compilation",
            getDefaultValue: () => true);

        var maxDegreeOfParallelismOption = new Option<int>(
            name: "-maxDegreeOfParallelism",
            description: "Maximum number of concurrent compilation tasks",
            getDefaultValue: () => 4);

        var continueBuildOnErrorOption = new Option<bool?>(
            name: "-continueBuildOnError",
            description: "Continue building even if errors are found");

        command.AddOption(appJsonPathOption);
        command.AddOption(packageCachePathOption);
        command.AddOption(suppressWarningsOption);
        command.AddOption(generateReportLayoutOption);
        command.AddOption(parallelOption);
        command.AddOption(maxDegreeOfParallelismOption);
        command.AddOption(continueBuildOnErrorOption);

        command.SetHandler(async (context) =>
        {
            var appJsonPath = context.ParseResult.GetValueForOption(appJsonPathOption)!;
            var packageCachePath = context.ParseResult.GetValueForOption(packageCachePathOption);
            var suppressWarnings = context.ParseResult.GetValueForOption(suppressWarningsOption);
            var generateReportLayout = context.ParseResult.GetValueForOption(generateReportLayoutOption);
            var parallel = context.ParseResult.GetValueForOption(parallelOption);
            var maxDegreeOfParallelism = context.ParseResult.GetValueForOption(maxDegreeOfParallelismOption);
            var continueBuildOnError = context.ParseResult.GetValueForOption(continueBuildOnErrorOption);

            // Validate maxDegreeOfParallelism
            if (maxDegreeOfParallelism <= 0)
            {
                Console.Error.WriteLine("Error: -maxDegreeOfParallelism must be greater than 0");
                context.ExitCode = 1;
                return;
            }

            await ExecuteAsync(appJsonPath, packageCachePath, suppressWarnings,
                generateReportLayout, parallel, maxDegreeOfParallelism, continueBuildOnError);
        });

        return command;
    }

    private static async Task ExecuteAsync(
        string appJsonPath,
        string? packageCachePath,
        bool suppressWarnings,
        bool generateReportLayout,
        bool parallel,
        int maxDegreeOfParallelism,
        bool? continueBuildOnError)
    {
        // Auto-download compiler based on app.json platform version
        var artifactService = new ArtifactService();
        var version = await artifactService.ResolveVersionFromAppJsonAsync(appJsonPath);
        await artifactService.EnsureArtifactsAsync(version);
        var compilerPath = artifactService.GetCachedCompilerPath(version)
            ?? throw new InvalidOperationException($"Failed to get compiler path for version {version}");

        var compilerService = new CompilerService();
        var result = await compilerService.CompileAsync(
            appJsonPath, compilerPath, packageCachePath, suppressWarnings,
            generateReportLayout, parallel, maxDegreeOfParallelism, continueBuildOnError);

        // Output result as JSON
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, JsonContext.Default.CompileResult));

        Environment.ExitCode = result.Success ? 0 : 1;
    }
}
