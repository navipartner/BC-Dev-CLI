using BCDev.Auth;
using BCDev.BC;
using BCDev.Models;

namespace BCDev.Services;

/// <summary>
/// Service for running tests against Business Central
/// </summary>
public class TestService
{
    /// <summary>
    /// Result of a test run
    /// </summary>
    public class TestRunResult
    {
        public bool Success { get; set; }
        public int TotalTests { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public string? Duration { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public List<TestMethodResultDto> Results { get; set; } = new();
    }

    public class TestMethodResultDto
    {
        public string Codeunit { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string? Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }
    }

    /// <summary>
    /// Run tests against Business Central
    /// </summary>
    public async Task<TestRunResult> RunTestsAsync(
        string launchJsonPath,
        string launchJsonName,
        string? username,
        string? password,
        int? codeunitId,
        string? methodName,
        bool testAll,
        string testSuite,
        string? bcClientDllPath,
        int timeoutMinutes)
    {
        var result = new TestRunResult();
        var startTime = DateTime.Now;

        try
        {
            // Setup assembly resolver for BC client DLL
            var dllPath = bcClientDllPath ?? AssemblyResolver.GetDefaultLibsPath();
            AssemblyResolver.SetupAssemblyResolve("Microsoft.Dynamics", dllPath);

            // Load launch configuration
            var launchConfigService = new LaunchConfigService();
            var config = launchConfigService.GetConfiguration(launchJsonPath, launchJsonName);

            // Get credentials
            var credentialProvider = await GetCredentialProviderAsync(config, username, password);
            var credentials = await credentialProvider.GetCredentialsAsync();

            // Build service URL
            var serviceUrl = config.GetClientServicesUrl();
            var timeout = TimeSpan.FromMinutes(timeoutMinutes);

            // Create test runner and run tests
            using var runner = new TestRunner(serviceUrl, credentialProvider.AuthenticationScheme,
                credentials, timeout);

            // Build test filters
            string? testCodeunitsRange = codeunitId?.ToString();
            string? testProcedureRange = methodName;

            // Setup the test run
            runner.SetupTestRun(
                testPage: TestRunner.DefaultTestPage,
                testSuite: testSuite,
                extensionId: null,
                testCodeunitsRange: testCodeunitsRange,
                testProcedureRange: testProcedureRange,
                testAll: testAll);

            // Run all tests
            var testResults = runner.RunAllTests();

            // Convert results to output format
            foreach (var testResult in testResults)
            {
                foreach (var methodResult in testResult.TestResults)
                {
                    var dto = new TestMethodResultDto
                    {
                        Codeunit = methodResult.CodeUnit,
                        Function = methodResult.Method,
                        Result = GetResultString(methodResult.Result),
                        ErrorMessage = methodResult.Message,
                        StackTrace = methodResult.StackTrace
                    };

                    // Calculate duration if times are available
                    if (DateTime.TryParse(methodResult.StartTime, out var start) &&
                        DateTime.TryParse(methodResult.FinishTime, out var finish))
                    {
                        dto.Duration = (finish - start).ToString(@"hh\:mm\:ss\.fff");
                    }

                    result.Results.Add(dto);

                    // Update counters
                    result.TotalTests++;
                    switch (methodResult.Result)
                    {
                        case "2": // Success
                            result.Passed++;
                            break;
                        case "1": // Failure
                            result.Failed++;
                            break;
                        case "3": // Skipped
                            result.Skipped++;
                            break;
                    }
                }
            }

            result.Success = result.Failed == 0;
            result.Message = result.Failed == 0
                ? "All tests passed"
                : $"{result.Failed} test(s) failed";
            result.Duration = (DateTime.Now - startTime).ToString(@"hh\:mm\:ss");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = "Test execution failed";
            result.Error = ex.Message;
            result.Duration = (DateTime.Now - startTime).ToString(@"hh\:mm\:ss");
        }

        return result;
    }

    private static async Task<ICredentialProvider> GetCredentialProviderAsync(
        LaunchConfiguration config, string? username, string? password)
    {
        if (config.Authentication == AuthenticationMethod.UserPassword)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    "Username and password are required for UserPassword authentication");
            }
            return new NavUserPasswordProvider(username, password);
        }

        if (config.Authentication == AuthenticationMethod.AAD ||
            config.Authentication == AuthenticationMethod.MicrosoftEntraID)
        {
            var authority = $"https://login.microsoftonline.com/common";
            var scopes = new[] { $"{config.Server}/.default" };
            return new AadAuthProvider(authority, scopes, username: username, password: password);
        }

        throw new NotSupportedException($"Authentication method {config.Authentication} is not supported");
    }

    private static string GetResultString(string resultCode)
    {
        return resultCode switch
        {
            "1" => "Fail",
            "2" => "Pass",
            "3" => "Skip",
            _ => "Unknown"
        };
    }
}
