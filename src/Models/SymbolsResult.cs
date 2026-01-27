namespace BCDev.Models;

/// <summary>
/// Result of a symbols download operation
/// </summary>
public class SymbolsResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public List<string> DownloadedSymbols { get; set; } = new();
    public List<SymbolFailure> Failures { get; set; } = new();
}

/// <summary>
/// Information about a failed symbol download
/// </summary>
public class SymbolFailure
{
    public string Symbol { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
