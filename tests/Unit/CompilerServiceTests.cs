using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class CompilerServiceTests
{
    private readonly CompilerService _service;

    public CompilerServiceTests()
    {
        _service = new CompilerService();
    }

    [Fact]
    public async Task CompileAsync_AppJsonNotFound_ReturnsFailure()
    {
        var result = await _service.CompileAsync(
            "/nonexistent/path/app.json",
            "/some/compiler/path",
            null);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task CompileAsync_CompilerNotFound_ReturnsFailure()
    {
        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var appJsonPath = Path.Combine(fixturesPath, "valid-app.json");

        var result = await _service.CompileAsync(
            appJsonPath,
            "/nonexistent/compiler/alc.exe",
            null);

        Assert.False(result.Success);
        Assert.Contains("Compiler not found", result.Message);
    }

    [Theory]
    [InlineData(true, "/generatereportlayout+")]
    [InlineData(false, "/generatereportlayout-")]
    public void BuildArguments_GenerateReportLayout_AddsFlag(bool value, string expected)
    {
        var args = CompilerService.BuildCompilerArguments(
            projectPath: "/project",
            outputPath: "/out.app",
            packageCachePath: null,
            generateReportLayout: value,
            parallel: null,
            maxDegreeOfParallelism: null,
            continueBuildOnError: null);

        Assert.Contains(expected, args);
    }

    [Theory]
    [InlineData(true, "/parallel+")]
    [InlineData(false, "/parallel-")]
    public void BuildArguments_Parallel_AddsFlag(bool value, string expected)
    {
        var args = CompilerService.BuildCompilerArguments(
            projectPath: "/project",
            outputPath: "/out.app",
            packageCachePath: null,
            generateReportLayout: null,
            parallel: value,
            maxDegreeOfParallelism: null,
            continueBuildOnError: null);

        Assert.Contains(expected, args);
    }

    [Theory]
    [InlineData(1, "/maxdegreeofparallelism:1")]
    [InlineData(4, "/maxdegreeofparallelism:4")]
    [InlineData(8, "/maxdegreeofparallelism:8")]
    public void BuildArguments_MaxDegreeOfParallelism_AddsFlag(int value, string expected)
    {
        var args = CompilerService.BuildCompilerArguments(
            projectPath: "/project",
            outputPath: "/out.app",
            packageCachePath: null,
            generateReportLayout: null,
            parallel: null,
            maxDegreeOfParallelism: value,
            continueBuildOnError: null);

        Assert.Contains(expected, args);
    }

    [Theory]
    [InlineData(true, "/continuebuildonerror+")]
    [InlineData(false, "/continuebuildonerror-")]
    public void BuildArguments_ContinueBuildOnError_AddsFlag(bool value, string expected)
    {
        var args = CompilerService.BuildCompilerArguments(
            projectPath: "/project",
            outputPath: "/out.app",
            packageCachePath: null,
            generateReportLayout: null,
            parallel: null,
            maxDegreeOfParallelism: null,
            continueBuildOnError: value);

        Assert.Contains(expected, args);
    }

    [Fact]
    public void BuildArguments_NullOptions_OmitsFlags()
    {
        var args = CompilerService.BuildCompilerArguments(
            projectPath: "/project",
            outputPath: "/out.app",
            packageCachePath: null,
            generateReportLayout: null,
            parallel: null,
            maxDegreeOfParallelism: null,
            continueBuildOnError: null);

        Assert.DoesNotContain("/generatereportlayout", args);
        Assert.DoesNotContain("/parallel", args);
        Assert.DoesNotContain("/maxdegreeofparallelism", args);
        Assert.DoesNotContain("/continuebuildonerror", args);
    }

    [Fact]
    public void BuildArguments_AllOptions_AddsAllFlags()
    {
        var args = CompilerService.BuildCompilerArguments(
            projectPath: "/project",
            outputPath: "/out.app",
            packageCachePath: "/packages",
            generateReportLayout: true,
            parallel: true,
            maxDegreeOfParallelism: 4,
            continueBuildOnError: false);

        Assert.Contains("/project:", args);
        Assert.Contains("/out:", args);
        Assert.Contains("/packagecachepath:", args);
        Assert.Contains("/generatereportlayout+", args);
        Assert.Contains("/parallel+", args);
        Assert.Contains("/maxdegreeofparallelism:4", args);
        Assert.Contains("/continuebuildonerror-", args);
    }

    [Fact]
    public void BuildArguments_WithDefaults_ProducesExpectedFlags()
    {
        // These are the new defaults: parallel=true, maxDegreeOfParallelism=4, generateReportLayout=false
        var args = CompilerService.BuildCompilerArguments(
            projectPath: "/project",
            outputPath: "/out.app",
            packageCachePath: null,
            generateReportLayout: false,
            parallel: true,
            maxDegreeOfParallelism: 4,
            continueBuildOnError: null);

        Assert.Contains("/parallel+", args);
        Assert.Contains("/maxdegreeofparallelism:4", args);
        Assert.Contains("/generatereportlayout-", args);
        Assert.DoesNotContain("/continuebuildonerror", args);
    }
}
