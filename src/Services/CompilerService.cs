using System.Diagnostics;
using System.Text.Json;
using BCDev.Models;

namespace BCDev.Services;

/// <summary>
/// Service for compiling AL applications using alc.exe
/// </summary>
public class CompilerService
{
    /// <summary>
    /// Result of a compilation operation
    /// </summary>
    public class CompileResult
    {
        public bool Success { get; set; }
        public string? AppPath { get; set; }
        public string? Message { get; set; }
        public List<CompilerError> Errors { get; set; } = new();
        public List<CompilerError> Warnings { get; set; } = new();
        public int ExitCode { get; set; }
        public string? StdOut { get; set; }
        public string? StdErr { get; set; }
    }

    public class CompilerError
    {
        public string? File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Compile an AL application
    /// </summary>
    /// <param name="appJsonPath">Path to app.json file</param>
    /// <param name="compilerPath">Path to alc.exe compiler</param>
    /// <param name="packageCachePath">Path to .alpackages folder</param>
    /// <param name="suppressWarnings">If true, warnings are not included in the result</param>
    public async Task<CompileResult> CompileAsync(string appJsonPath, string compilerPath, string? packageCachePath, bool suppressWarnings = false)
    {
        var result = new CompileResult();

        // Validate inputs
        if (!File.Exists(appJsonPath))
        {
            result.Success = false;
            result.Message = $"app.json not found: {appJsonPath}";
            return result;
        }

        if (!File.Exists(compilerPath))
        {
            result.Success = false;
            result.Message = $"Compiler not found: {compilerPath}";
            return result;
        }

        // Parse app.json
        AppJson appJson;
        try
        {
            var json = await File.ReadAllTextAsync(appJsonPath);
            appJson = JsonSerializer.Deserialize(json, JsonContext.Default.AppJson)
                ?? throw new InvalidOperationException("Failed to parse app.json");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to parse app.json: {ex.Message}";
            return result;
        }

        var appFolder = Path.GetDirectoryName(appJsonPath) ?? throw new InvalidOperationException("Invalid app.json path");
        var outputPath = Path.Combine(appFolder, appJson.GetAppFileName());

        // Default package cache path is .alpackages in app folder
        if (string.IsNullOrEmpty(packageCachePath))
        {
            packageCachePath = Path.Combine(appFolder, ".alpackages");
        }

        // Build compiler arguments
        var args = new List<string>
        {
            $"/project:\"{appFolder}\"",
            $"/out:\"{outputPath}\""
        };

        if (Directory.Exists(packageCachePath))
        {
            args.Add($"/packagecachepath:\"{packageCachePath}\"");
        }

        // Execute compiler
        var startInfo = new ProcessStartInfo
        {
            FileName = compilerPath,
            Arguments = string.Join(" ", args),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                result.Success = false;
                result.Message = "Failed to start compiler process";
                return result;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            result.ExitCode = process.ExitCode;

            // Filter warnings from raw output if suppressing
            if (suppressWarnings)
            {
                result.StdOut = FilterWarningsFromOutput(stdout);
                result.StdErr = FilterWarningsFromOutput(stderr);
            }
            else
            {
                result.StdOut = stdout;
                result.StdErr = stderr;
            }

            // Parse compiler output for errors and warnings
            ParseCompilerOutput(stdout + stderr, result, suppressWarnings);

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                result.Success = true;
                result.AppPath = outputPath;
                result.Message = $"Compilation successful: {outputPath}";
            }
            else
            {
                result.Success = false;
                result.Message = $"Compilation failed with exit code {process.ExitCode}";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Compiler execution failed: {ex.Message}";
        }

        return result;
    }

    private void ParseCompilerOutput(string output, CompileResult result, bool suppressWarnings)
    {
        // Parse compiler output lines for errors and warnings
        // Format: file(line,column): error/warning CODE: message
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmed = line.Trim();

            // Check for error pattern
            if (trimmed.Contains(": error "))
            {
                var error = ParseCompilerLine(trimmed);
                if (error != null)
                {
                    result.Errors.Add(error);
                }
            }
            else if (trimmed.Contains(": warning ") && !suppressWarnings)
            {
                var warning = ParseCompilerLine(trimmed);
                if (warning != null)
                {
                    result.Warnings.Add(warning);
                }
            }
        }
    }

    private CompilerError? ParseCompilerLine(string line)
    {
        // Pattern: file(line,column): error/warning CODE: message
        try
        {
            var error = new CompilerError();

            // Find the position marker (line,column)
            var parenStart = line.IndexOf('(');
            var parenEnd = line.IndexOf(')');

            if (parenStart > 0 && parenEnd > parenStart)
            {
                error.File = line[..parenStart];
                var posStr = line[(parenStart + 1)..parenEnd];
                var posParts = posStr.Split(',');
                if (posParts.Length >= 2)
                {
                    int.TryParse(posParts[0], out var lineNum);
                    int.TryParse(posParts[1], out var colNum);
                    error.Line = lineNum;
                    error.Column = colNum;
                }
            }

            // Extract error code and message
            var colonIdx = line.IndexOf(':', parenEnd > 0 ? parenEnd : 0);
            if (colonIdx > 0)
            {
                var afterColon = line[(colonIdx + 1)..].Trim();
                var parts = afterColon.Split(':', 2);
                if (parts.Length >= 2)
                {
                    var typeAndCode = parts[0].Trim();
                    error.Message = parts[1].Trim();

                    // Extract code from "error AL0001" or "warning AL0002"
                    var codeParts = typeAndCode.Split(' ', 2);
                    if (codeParts.Length >= 2)
                    {
                        error.Code = codeParts[1];
                    }
                }
                else
                {
                    error.Message = afterColon;
                }
            }

            return error;
        }
        catch
        {
            return new CompilerError { Message = line };
        }
    }

    /// <summary>
    /// Filter warning lines from compiler output text.
    /// Handles both Unix (\n) and Windows (\r\n) line endings.
    /// </summary>
    public static string? FilterWarningsFromOutput(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return output;

        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var filteredLines = lines.Where(line => !line.Contains(": warning ")).ToArray();
        return string.Join("\n", filteredLines);
    }

    /// <summary>
    /// Build compiler command-line arguments string
    /// </summary>
    public static string BuildCompilerArguments(
        string projectPath,
        string outputPath,
        string? packageCachePath,
        bool? generateReportLayout,
        bool? parallel,
        int? maxDegreeOfParallelism,
        bool? continueBuildOnError)
    {
        var args = new List<string>
        {
            $"/project:\"{projectPath}\"",
            $"/out:\"{outputPath}\""
        };

        if (!string.IsNullOrEmpty(packageCachePath))
        {
            args.Add($"/packagecachepath:\"{packageCachePath}\"");
        }

        if (generateReportLayout.HasValue)
        {
            args.Add($"/generatereportlayout{(generateReportLayout.Value ? "+" : "-")}");
        }

        if (parallel.HasValue)
        {
            args.Add($"/parallel{(parallel.Value ? "+" : "-")}");
        }

        if (maxDegreeOfParallelism.HasValue)
        {
            args.Add($"/maxdegreeofparallelism:{maxDegreeOfParallelism.Value}");
        }

        if (continueBuildOnError.HasValue)
        {
            args.Add($"/continuebuildonerror{(continueBuildOnError.Value ? "+" : "-")}");
        }

        return string.Join(" ", args);
    }
}
