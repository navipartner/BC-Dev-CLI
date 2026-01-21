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

        var compilerPathOption = new Option<string>(
            name: "-compilerPath",
            description: "Path to alc.exe compiler")
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
        command.AddOption(compilerPathOption);
        command.AddOption(packageCachePathOption);
        command.AddOption(suppressWarningsOption);

        command.SetHandler(async (appJsonPath, compilerPath, packageCachePath, suppressWarnings) =>
        {
            await ExecuteAsync(appJsonPath, compilerPath, packageCachePath, suppressWarnings);
        }, appJsonPathOption, compilerPathOption, packageCachePathOption, suppressWarningsOption);

        return command;
    }

    private static async Task ExecuteAsync(string appJsonPath, string compilerPath, string? packageCachePath, bool suppressWarnings)
    {
        var compilerService = new CompilerService();
        var result = await compilerService.CompileAsync(appJsonPath, compilerPath, packageCachePath, suppressWarnings);

        // Output result as JSON
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        }));

        Environment.ExitCode = result.Success ? 0 : 1;
    }
}
