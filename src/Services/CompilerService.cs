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
    public async Task<CompileResult> CompileAsync(string appJsonPath, string compilerPath, string? packageCachePath)
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
            appJson = JsonSerializer.Deserialize<AppJson>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse app.json");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to parse app.json: {ex.Message}";
            return result;
        }

        var appFolder = Path.GetDirectoryName(appJsonPath) ?? throw new InvalidOperationException("Invalid app.json path");
        var outputPath = Path.Combine(appFolder, appJson.GetAppFileName());

        // Default package cache path
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
            result.StdOut = stdout;
            result.StdErr = stderr;

            // Parse compiler output for errors and warnings
            ParseCompilerOutput(stdout + stderr, result);

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

    private void ParseCompilerOutput(string output, CompileResult result)
    {
        // Parse compiler output lines for errors and warnings
        // Format: file(line,column): error/warning CODE: message
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmed = line.Trim();

            // Check for error pattern
            if (trimmed.Contains(": error ") || trimmed.Contains(": warning "))
            {
                var error = ParseCompilerLine(trimmed);
                if (error != null)
                {
                    if (trimmed.Contains(": error "))
                        result.Errors.Add(error);
                    else
                        result.Warnings.Add(error);
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
}
