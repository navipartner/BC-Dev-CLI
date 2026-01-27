using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BCDev.BC;

/// <summary>
/// Test runner for executing AL tests via Business Central's test tool page.
/// Uses late binding to avoid compile-time dependency on BC client DLL.
/// </summary>
public class TestRunner : ClientContext
{
    public const string AllTestsExecutedString = "All tests executed.";
    public const string DefaultTestSuite = "DEFAULT";
    public const int DefaultTestPage = 130455;
    public const int DefaultTestRunnerCodeunit = 130450;
    public const string DateTimeFormat = "s"; // ISO 8601
    public const int FailureResult = 1;
    public const int SuccessResult = 2;
    public const int SkippedResult = 3;
    public const int MaxUnexpectedFailures = 50;

    public int TestPage { get; private set; }
    public string TestSuite { get; private set; } = DefaultTestSuite;

    public TestRunner(string serviceUrl, string authenticationScheme, ICredentials credential,
        TimeSpan interactionTimeout, string culture = "en-US")
        : base(serviceUrl, authenticationScheme, credential, interactionTimeout, culture)
    {
    }

    /// <summary>
    /// Setup the test run with filters and configuration.
    /// When testAll is true or testCodeunitsRange is specified, the test suite is auto-populated.
    /// </summary>
    public void SetupTestRun(
        int testPage = DefaultTestPage,
        string testSuite = DefaultTestSuite,
        string? extensionId = null,
        string? testCodeunitsRange = null,
        string? testProcedureRange = null,
        bool testAll = false,
        int testRunnerCodeunit = DefaultTestRunnerCodeunit)
    {
        TestPage = testPage;
        TestSuite = testSuite;

        var form = OpenTestForm(TestPage);
        SetTestSuite(form, TestSuite);
        SetTestRunner(form, testRunnerCodeunit);

        // Auto-populate the test suite based on filters
        // Setting TestCodeunitRangeFilter triggers OnValidate which populates the suite
        if (testAll)
        {
            // For testAll, use a range that matches all possible codeunit IDs
            // BC codeunit IDs range from 1 to 2147483647 (max int)
            SetTestCodeunits(form, "1..2147483647");
        }
        else if (!string.IsNullOrEmpty(testCodeunitsRange))
        {
            // Setting the codeunit range filter auto-populates the suite via OnValidate
            SetTestCodeunits(form, testCodeunitsRange);
        }

        // Set extension filter if provided (this also triggers auto-populate via OnValidate)
        if (!string.IsNullOrEmpty(extensionId))
        {
            SetExtensionId(form, extensionId);
        }

        // Set procedure filter to narrow down which test methods to run
        if (!string.IsNullOrEmpty(testProcedureRange))
        {
            SetTestProcedures(form, testProcedureRange);
        }

        ClearTestResults(form);
    }

    /// <summary>
    /// Run the next test in the queue
    /// </summary>
    /// <returns>Test result, or null if all tests have been executed</returns>
    public TestRunnerResult? RunNextTest()
    {
        OpenTestForm(TestPage);
        SetTestSuite(OpenedForm!, TestSuite);

        InvokeAction(GetActionByName(OpenedForm!, "RunNextTest"));

        var testResultControl = GetControlByName(OpenedForm!, "TestResultJson");
        string resultString = testResultControl.StringValue;

        if (resultString == AllTestsExecutedString)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TestRunnerResult>(resultString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
        });
    }

    /// <summary>
    /// Run all tests and collect results
    /// </summary>
    public List<TestRunnerResult> RunAllTests()
    {
        var results = new List<TestRunnerResult>();
        var unexpectedFailures = 0;
        Exception? firstException = null;

        while (unexpectedFailures < MaxUnexpectedFailures)
        {
            var testStartTime = DateTime.Now;
            TestRunnerResult? result;

            try
            {
                result = RunNextTest();

                if (result == null)
                {
                    // All tests completed
                    return results;
                }

                results.Add(result);
            }
            catch (Exception ex)
            {
                unexpectedFailures++;
                firstException ??= ex;

                // Record the unexpected failure
                var failureResult = new TestRunnerResult
                {
                    Name = "Unexpected Failure",
                    CodeUnit = "UnexpectedFailure",
                    StartTime = testStartTime.ToString(DateTimeFormat),
                    FinishTime = DateTime.Now.ToString(DateTimeFormat),
                    Result = FailureResult.ToString(),
                    TestResults = new List<TestMethodResultData>
                    {
                        new TestMethodResultData
                        {
                            Method = "Unexpected Failure",
                            CodeUnit = "Unexpected Failure",
                            StartTime = testStartTime.ToString(DateTimeFormat),
                            FinishTime = DateTime.Now.ToString(DateTimeFormat),
                            Result = FailureResult.ToString(),
                            Message = ex.Message,
                            StackTrace = ex.StackTrace
                        }
                    }
                };

                results.Add(failureResult);
            }
        }

        throw new Exception(
            $"Test execution aborted after {MaxUnexpectedFailures} unexpected failures.",
            firstException);
    }

    public override void CloseSession()
    {
        TestPage = 0;
        TestSuite = "";
        base.CloseSession();
    }

    private dynamic OpenTestForm(int testPage)
    {
        var form = OpenForm(testPage);
        if (form == null)
        {
            throw new Exception(
                $"Cannot open test page {testPage}. Verify the test tool and test objects are installed.");
        }
        return form;
    }

    private void SetTestSuite(dynamic form, string testSuite)
    {
        var control = GetControlByName(form, "CurrentSuiteName");
        SaveValue(control, testSuite);
    }

    private void SetExtensionId(dynamic form, string extensionId)
    {
        var control = GetControlByName(form, "ExtensionId");
        SaveValue(control, extensionId);
    }

    private void SetTestCodeunits(dynamic form, string filter)
    {
        var control = GetControlByName(form, "TestCodeunitRangeFilter");
        SaveValue(control, filter);
    }

    private void SetTestProcedures(dynamic form, string filter)
    {
        var control = GetControlByName(form, "TestProcedureRangeFilter");
        SaveValue(control, filter);
    }

    private void SetTestRunner(dynamic form, int testRunnerId)
    {
        if (testRunnerId == 0) return;

        var control = GetControlByName(form, "TestRunnerCodeunitId");
        SaveValue(control, testRunnerId.ToString());
    }

    private void ClearTestResults(dynamic form)
    {
        InvokeAction(GetActionByName(form, "ClearTestResults"));
    }
}

/// <summary>
/// Represents a test result from BC
/// </summary>
public class TestRunnerResult
{
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(StringOrIntConverter))]
    public string CodeUnit { get; set; } = string.Empty;

    public string StartTime { get; set; } = string.Empty;
    public string FinishTime { get; set; } = string.Empty;

    [JsonConverter(typeof(StringOrIntConverter))]
    public string Result { get; set; } = string.Empty;

    public List<TestMethodResultData> TestResults { get; set; } = new();
}

/// <summary>
/// Represents a single test method result
/// </summary>
public class TestMethodResultData
{
    public string Method { get; set; } = string.Empty;

    [JsonConverter(typeof(StringOrIntConverter))]
    public string CodeUnit { get; set; } = string.Empty;

    public string StartTime { get; set; } = string.Empty;
    public string FinishTime { get; set; } = string.Empty;

    [JsonConverter(typeof(StringOrIntConverter))]
    public string Result { get; set; } = string.Empty;

    public string? Message { get; set; }
    public string? StackTrace { get; set; }
}

/// <summary>
/// JSON converter that handles both string and integer values, converting to string
/// </summary>
public class StringOrIntConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.GetInt64().ToString(),
            _ => string.Empty
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
