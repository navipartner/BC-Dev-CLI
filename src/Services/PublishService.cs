using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BCDev.Auth;
using BCDev.Models;

namespace BCDev.Services;

/// <summary>
/// Service for publishing AL applications to Business Central via Development Service API
/// </summary>
public class PublishService
{
    /// <summary>
    /// Result of a publish operation
    /// </summary>
    public class PublishResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? AppPath { get; set; }
        public string? Error { get; set; }
        public string? OperationId { get; set; }
    }

    /// <summary>
    /// Publish an AL application to Business Central
    /// </summary>
    public async Task<PublishResult> PublishAsync(
        bool recompile,
        string? appPath,
        string? appJsonPath,
        string? compilerPath,
        string? packageCachePath,
        string launchJsonPath,
        string launchJsonName,
        string? username,
        string? password,
        string? bcClientDllPath)
    {
        var result = new PublishResult();

        try
        {
            // If recompile flag is set, compile first (suppress warnings for cleaner output)
            if (recompile)
            {
                var compilerService = new CompilerService();
                var compileResult = await compilerService.CompileAsync(appJsonPath!, compilerPath!, packageCachePath, suppressWarnings: true);

                if (!compileResult.Success)
                {
                    result.Success = false;
                    result.Message = "Compilation failed";
                    result.Error = compileResult.Message;
                    return result;
                }

                appPath = compileResult.AppPath;
            }

            // Validate app path
            if (string.IsNullOrEmpty(appPath) || !File.Exists(appPath))
            {
                result.Success = false;
                result.Message = "App file not found";
                result.Error = $"The app file does not exist: {appPath}";
                return result;
            }

            result.AppPath = appPath;

            // Load launch configuration
            var launchConfigService = new LaunchConfigService();
            var config = launchConfigService.GetConfiguration(launchJsonPath, launchJsonName);

            // Get credentials
            var credentialProvider = await GetCredentialProviderAsync(config, username, password);
            var credentials = await credentialProvider.GetCredentialsAsync();

            // Build the Development Service URL
            var devServiceUrl = BuildDevServiceUrl(config);

            // Publish the app
            await PublishAppAsync(devServiceUrl, appPath, config, credentials, credentialProvider.AuthenticationScheme, result);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = "Publish failed";
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task PublishAppAsync(
        string devServiceUrl,
        string appPath,
        LaunchConfiguration config,
        ICredentials credentials,
        string authScheme,
        PublishResult result)
    {
        // Create HTTP client with credentials
        var handler = new HttpClientHandler
        {
            Credentials = credentials,
            PreAuthenticate = true,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromMinutes(10); // Allow time for large apps

        // Set up authentication header
        if (authScheme == "AzureActiveDirectory" && credentials is TokenCredential tokenCred)
        {
            var networkCred = tokenCred.GetCredential(new Uri(devServiceUrl), "Bearer");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", networkCred.Password);
        }
        else if (credentials is NetworkCredential netCred)
        {
            // BC Dev Service requires Basic auth header to be set explicitly
            var authBytes = Encoding.ASCII.GetBytes($"{netCred.UserName}:{netCred.Password}");
            var authBase64 = Convert.ToBase64String(authBytes);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authBase64);
        }

        // Read the app file
        var appBytes = await File.ReadAllBytesAsync(appPath);
        var appFileName = Path.GetFileName(appPath);

        // Build the publish endpoint URL
        // BC Development Service endpoint: /dev/apps
        var publishUrl = $"{devServiceUrl}apps";

        // Add query parameters for tenant and schema update mode
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(config.Tenant))
        {
            queryParams.Add($"tenant={Uri.EscapeDataString(config.Tenant)}");
        }
        // Use SchemaUpdateMode value from launch.json configuration
        queryParams.Add($"SchemaUpdateMode={config.SchemaUpdateMode}");

        if (queryParams.Count > 0)
        {
            publishUrl += "?" + string.Join("&", queryParams);
        }

        // Create multipart form data content
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(appBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", appFileName);

        try
        {
            Console.Error.WriteLine($"Publishing to: {publishUrl}");
            var response = await client.PostAsync(publishUrl, content);

            if (response.IsSuccessStatusCode)
            {
                result.Success = true;
                result.Message = $"App published successfully: {appFileName}";

                // Try to get operation ID from response
                var responseContent = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(responseContent))
                {
                    result.OperationId = responseContent;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                result.Success = false;
                result.Message = $"Publish failed with status {(int)response.StatusCode}";
                result.Error = errorContent;
            }
        }
        catch (HttpRequestException ex)
        {
            result.Success = false;
            result.Message = "HTTP request failed";
            result.Error = ex.Message;
        }
    }

    private static string BuildDevServiceUrl(LaunchConfiguration config)
    {
        // Use the configuration's built-in method which handles both OnPrem and SaaS
        return config.GetDevServicesUrl();
    }

    private static Task<ICredentialProvider> GetCredentialProviderAsync(
        LaunchConfiguration config,
        string? username,
        string? password)
    {
        // Use auth type from launch.json config
        var effectiveAuthType = config.Authentication == AuthenticationMethod.UserPassword
            ? AuthType.UserPassword
            : AuthType.AAD;

        if (effectiveAuthType == AuthType.UserPassword)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    "Username and password are required for UserPassword authentication");
            }
            return Task.FromResult<ICredentialProvider>(new NavUserPasswordProvider(username, password));
        }

        // AAD auth - use tenant-specific authority if we have a tenant domain
        var authority = !string.IsNullOrEmpty(config.PrimaryTenantDomain)
            ? $"https://login.microsoftonline.com/{config.PrimaryTenantDomain}"
            : "https://login.microsoftonline.com/common";
        var scopes = new[] { $"{config.GetServerForScope()}/.default" };
        return Task.FromResult<ICredentialProvider>(new AadAuthProvider(authority, scopes, username: username, password: password));
    }
}
