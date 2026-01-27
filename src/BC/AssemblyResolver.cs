using System.Reflection;

namespace BCDev.BC;

/// <summary>
/// Handles dynamic assembly resolution for BC client DLLs
/// </summary>
public static class AssemblyResolver
{
    private static bool _initialized = false;
    private static string? _searchDirectory;

    /// <summary>
    /// Setup assembly resolution to search for BC client DLLs in the specified directory
    /// </summary>
    /// <param name="searchPattern">Pattern to match (e.g., "Microsoft.Dynamics")</param>
    /// <param name="directoryPath">Directory to search for assemblies</param>
    public static void SetupAssemblyResolve(string searchPattern, string? directoryPath = null)
    {
        if (_initialized) return;

        _searchDirectory = directoryPath ?? GetDefaultLibsPath();

        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            if (args.Name.Contains(searchPattern))
            {
                var assemblyFileName = args.Name.Split(',')[0];
                var filePath = FindFileInDirectory($"{assemblyFileName}.dll", _searchDirectory);
                if (filePath != null)
                {
                    return Assembly.LoadFrom(filePath);
                }
            }
            return null;
        };

        _initialized = true;
    }

    /// <summary>
    /// Get the default libs path. Checks in order:
    /// 1. Local libs folder (next to executable)
    /// 2. BC artifact cache (any cached version)
    /// </summary>
    public static string GetDefaultLibsPath()
    {
        // First try local libs folder
        var basePath = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(basePath))
        {
            basePath = Environment.CurrentDirectory;
        }
        var localLibs = Path.Combine(basePath, "libs");
        if (Directory.Exists(localLibs) && Directory.GetFiles(localLibs, "*.dll").Length > 0)
        {
            return localLibs;
        }

        // Fall back to BC artifact cache
        var cacheDir = GetBCCacheDirectory();
        if (Directory.Exists(cacheDir))
        {
            // Find any cached version that has the client DLL
            var versionDirs = Directory.GetDirectories(cacheDir);
            foreach (var versionDir in versionDirs)
            {
                var clientDll = Path.Combine(versionDir, "Microsoft.Dynamics.Framework.UI.Client.dll");
                if (File.Exists(clientDll))
                {
                    return versionDir;
                }
            }
        }

        // Return local libs path (will fail gracefully later if missing)
        return localLibs;
    }

    /// <summary>
    /// Get the BC artifact cache directory path
    /// </summary>
    public static string GetBCCacheDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "bcdev", "cache");
        }
        else
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".bcdev", "cache");
        }
    }

    private static string? FindFileInDirectory(string fileName, string? directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
        {
            return null;
        }

        var files = Directory.GetFiles(directoryPath, fileName, SearchOption.AllDirectories);
        return files.FirstOrDefault();
    }

    /// <summary>
    /// Load all assemblies matching a pattern from a directory
    /// </summary>
    public static void LoadAssembliesFromFolder(string searchPattern, string? directoryPath = null)
    {
        var loadedAssemblies = new HashSet<string>();
        directoryPath ??= GetDefaultLibsPath();

        if (!Directory.Exists(directoryPath)) return;

        var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!loadedAssemblies.Contains(fileName))
            {
                Assembly.LoadFrom(file);
                loadedAssemblies.Add(fileName);
            }
        }
    }
}
