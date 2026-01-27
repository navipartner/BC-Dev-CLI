using System.Reflection;
using BCDev.Services;

namespace BCDev.BC;

/// <summary>
/// Dynamically loads the BC client assembly at runtime.
/// Downloads from Microsoft if not in cache.
/// </summary>
public static class BCClientLoader
{
    private static Assembly? _clientAssembly;
    private static Assembly? _interactionsAssembly;
    private static readonly object _lock = new();
    private static bool _initialized;

    private const string ClientAssemblyName = "Microsoft.Dynamics.Framework.UI.Client";
    private const string InteractionsAssemblyName = "Microsoft.Dynamics.Framework.UI.Client.Interactions";
    private const string DefaultBCVersion = "27.0";
    private static string? _currentVersion;

    /// <summary>
    /// Gets or sets the BC version to use. Set this before any BC operations.
    /// </summary>
    public static string Version
    {
        get => _currentVersion ?? DefaultBCVersion;
        set => _currentVersion = value;
    }

    /// <summary>
    /// Ensures BC client assemblies are loaded, downloading if necessary.
    /// </summary>
    public static Task EnsureLoadedAsync(string? version = null)
    {
        var bcVersion = version ?? Version;

        // Reset if version changed
        if (_initialized && _currentVersion != bcVersion)
        {
            _initialized = false;
            _clientAssembly = null;
            _interactionsAssembly = null;
        }

        if (_initialized) return Task.CompletedTask;

        lock (_lock)
        {
            if (_initialized) return Task.CompletedTask;

            _currentVersion = bcVersion;
            var artifactService = new ArtifactService();

            // Download if not cached
            if (!artifactService.IsVersionCached(bcVersion))
            {
                Console.WriteLine($"BC client not found in cache. Downloading version {bcVersion}...");
                artifactService.EnsureArtifactsAsync(bcVersion).GetAwaiter().GetResult();
            }

            var dllPath = artifactService.GetCachedClientDllPath(bcVersion)
                ?? throw new InvalidOperationException($"BC client DLL not found after download for version {bcVersion}");

            var dllDir = Path.GetDirectoryName(dllPath)!;
            var interactionsDllPath = Path.Combine(dllDir, $"{InteractionsAssemblyName}.dll");

            // Load assemblies
            _clientAssembly = Assembly.LoadFrom(dllPath);

            if (File.Exists(interactionsDllPath))
            {
                _interactionsAssembly = Assembly.LoadFrom(interactionsDllPath);
            }

            _initialized = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a type from the BC client assembly.
    /// </summary>
    public static Type GetClientType(string typeName)
    {
        EnsureInitialized();
        return _clientAssembly!.GetType($"{ClientAssemblyName}.{typeName}")
            ?? throw new TypeLoadException($"Type {typeName} not found in {ClientAssemblyName}");
    }

    /// <summary>
    /// Gets a type from the BC client interactions assembly.
    /// </summary>
    public static Type GetInteractionType(string typeName)
    {
        EnsureInitialized();

        // Try interactions assembly first, fall back to main assembly
        var type = _interactionsAssembly?.GetType($"{InteractionsAssemblyName}.{typeName}");
        if (type != null) return type;

        type = _clientAssembly!.GetType($"{ClientAssemblyName}.Interactions.{typeName}");
        if (type != null) return type;

        throw new TypeLoadException($"Type {typeName} not found in BC client assemblies");
    }

    /// <summary>
    /// Creates an instance of a BC client type.
    /// </summary>
    public static dynamic CreateInstance(string typeName, params object[] args)
    {
        var type = GetClientType(typeName);
        return Activator.CreateInstance(type, args)!;
    }

    /// <summary>
    /// Creates an instance of a BC client interaction type.
    /// </summary>
    public static dynamic CreateInteraction(string typeName, params object[] args)
    {
        var type = GetInteractionType(typeName);
        return Activator.CreateInstance(type, args)!;
    }

    /// <summary>
    /// Gets an enum value from the BC client assembly.
    /// </summary>
    public static object GetEnumValue(string enumTypeName, string valueName)
    {
        var type = GetClientType(enumTypeName);
        return Enum.Parse(type, valueName);
    }

    /// <summary>
    /// Gets the ClientSession type.
    /// </summary>
    public static Type ClientSessionType => GetClientType("ClientSession");

    /// <summary>
    /// Gets the ClientSessionState enum type.
    /// </summary>
    public static Type ClientSessionStateType => GetClientType("ClientSessionState");

    /// <summary>
    /// Gets a ClientSessionState enum value.
    /// </summary>
    public static object GetSessionState(string stateName) => GetEnumValue("ClientSessionState", stateName);

    /// <summary>
    /// Resets the loader state, allowing reinitialization with a different version.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _initialized = false;
            _clientAssembly = null;
            _interactionsAssembly = null;
            _currentVersion = null;
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("BCClientLoader not initialized. Call EnsureLoadedAsync first.");
        }
    }
}
