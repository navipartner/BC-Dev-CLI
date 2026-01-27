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

        command.AddOption(appJsonPathOption);
        command.AddOption(packageCachePathOption);
        command.AddOption(suppressWarningsOption);

        command.SetHandler(async (appJsonPath, packageCachePath, suppressWarnings) =>
        {
            await ExecuteAsync(appJsonPath, packageCachePath, suppressWarnings);
        }, appJsonPathOption, packageCachePathOption, suppressWarningsOption);

        return command;
    }

    private static async Task ExecuteAsync(string appJsonPath, string? packageCachePath, bool suppressWarnings)
    {
        // Auto-download compiler based on app.json platform version
        var artifactService = new ArtifactService();
        var version = await artifactService.ResolveVersionFromAppJsonAsync(appJsonPath);
        await artifactService.EnsureArtifactsAsync(version);
        var compilerPath = artifactService.GetCachedCompilerPath(version)
            ?? throw new InvalidOperationException($"Failed to get compiler path for version {version}");

        var compilerService = new CompilerService();
        var result = await compilerService.CompileAsync(appJsonPath, compilerPath, packageCachePath, suppressWarnings);

        // Output result as JSON
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, JsonContext.Default.CompileResult));

        Environment.ExitCode = result.Success ? 0 : 1;
    }
}
