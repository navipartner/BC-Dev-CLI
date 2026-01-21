using System.Text.Json;
using BCDev.BC;
using Xunit;

namespace BCDev.Tests.Unit;

public class TestResultParsingTests
{
    [Fact]
    public void ParseTestRunnerResult_ValidJson_ParsesCorrectly()
    {
        var json = """
        {
            "name": "My Test Codeunit",
            "codeUnit": "50100",
            "startTime": "2024-01-01T10:00:00",
            "finishTime": "2024-01-01T10:00:05",
            "result": "2",
            "testResults": [
                {
                    "method": "TestMethod1",
                    "codeUnit": "50100",
                    "startTime": "2024-01-01T10:00:00",
                    "finishTime": "2024-01-01T10:00:02",
                    "result": "2"
                },
                {
                    "method": "TestMethod2",
                    "codeUnit": "50100",
                    "startTime": "2024-01-01T10:00:02",
                    "finishTime": "2024-01-01T10:00:05",
                    "result": "1",
                    "message": "Test failed",
                    "stackTrace": "at TestMethod2 line 10"
                }
            ]
        }
        """;

        var result = JsonSerializer.Deserialize<TestRunnerResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(result);
        Assert.Equal("My Test Codeunit", result.Name);
        Assert.Equal("50100", result.CodeUnit);
        Assert.Equal("2", result.Result);
        Assert.Equal(2, result.TestResults.Count);

        Assert.Equal("TestMethod1", result.TestResults[0].Method);
        Assert.Equal("2", result.TestResults[0].Result);
        Assert.Null(result.TestResults[0].Message);

        Assert.Equal("TestMethod2", result.TestResults[1].Method);
        Assert.Equal("1", result.TestResults[1].Result);
        Assert.Equal("Test failed", result.TestResults[1].Message);
        Assert.Equal("at TestMethod2 line 10", result.TestResults[1].StackTrace);
    }

    [Fact]
    public void ParseTestRunnerResult_NumericCodeUnit_ParsesAsString()
    {
        // BC sometimes returns codeUnit as number, sometimes as string
        var json = """
        {
            "name": "Test",
            "codeUnit": 50100,
            "startTime": "2024-01-01T10:00:00",
            "finishTime": "2024-01-01T10:00:05",
            "result": 2,
            "testResults": []
        }
        """;

        var result = JsonSerializer.Deserialize<TestRunnerResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(result);
        Assert.Equal("50100", result.CodeUnit);
        Assert.Equal("2", result.Result);
    }

    [Fact]
    public void StringOrIntConverter_HandlesString()
    {
        var json = """{"codeUnit": "50100", "name": "Test", "startTime": "", "finishTime": "", "result": "1", "testResults": []}""";

        var result = JsonSerializer.Deserialize<TestRunnerResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.Equal("50100", result!.CodeUnit);
    }

    [Fact]
    public void StringOrIntConverter_HandlesInteger()
    {
        var json = """{"codeUnit": 50100, "name": "Test", "startTime": "", "finishTime": "", "result": 1, "testResults": []}""";

        var result = JsonSerializer.Deserialize<TestRunnerResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.Equal("50100", result!.CodeUnit);
    }
}
