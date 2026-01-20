using System.Text.Json;
using System.Text.Json.Serialization;

namespace BCDev.Formatters;

/// <summary>
/// JSON formatter for consistent output across all commands
/// </summary>
public static class JsonResultFormatter
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Format an object as JSON for output
    /// </summary>
    public static string Format<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, DefaultOptions);
    }

    /// <summary>
    /// Format and write to stdout
    /// </summary>
    public static void WriteToConsole<T>(T obj)
    {
        Console.WriteLine(Format(obj));
    }

    /// <summary>
    /// Create a standardized error response
    /// </summary>
    public static string FormatError(string message, string? details = null)
    {
        var error = new ErrorResponse
        {
            Success = false,
            Error = message,
            Details = details
        };
        return Format(error);
    }

    /// <summary>
    /// Create a standardized success response
    /// </summary>
    public static string FormatSuccess(string message, object? data = null)
    {
        var response = new SuccessResponse
        {
            Success = true,
            Message = message,
            Data = data
        };
        return Format(response);
    }
}

/// <summary>
/// Standard error response structure
/// </summary>
public class ErrorResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// Standard success response structure
/// </summary>
public class SuccessResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}
