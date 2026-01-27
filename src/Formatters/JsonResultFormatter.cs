using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BCDev.Formatters;

/// <summary>
/// JSON formatter for consistent output across all commands
/// </summary>
public static class JsonResultFormatter
{
    /// <summary>
    /// Format an object as JSON for output using source-generated context
    /// </summary>
    public static string Format<T>(T obj, JsonTypeInfo<T> typeInfo)
    {
        return JsonSerializer.Serialize(obj, typeInfo);
    }

    /// <summary>
    /// Format and write to stdout using source-generated context
    /// </summary>
    public static void WriteToConsole<T>(T obj, JsonTypeInfo<T> typeInfo)
    {
        Console.WriteLine(Format(obj, typeInfo));
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
        return JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse);
    }

    /// <summary>
    /// Create a standardized success response
    /// </summary>
    public static string FormatSuccess(string message)
    {
        var response = new SuccessResponse
        {
            Success = true,
            Message = message
        };
        return JsonSerializer.Serialize(response, JsonContext.Default.SuccessResponse);
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
}
