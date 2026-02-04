using System.CommandLine;
using BCDev.Commands;
using Xunit;

namespace BCDev.Tests.Unit;

public class CompileCommandTests
{
    [Fact]
    public void CompileCommand_ParallelDefaultsToTrue()
    {
        var command = CompileCommand.Create();
        var option = (Option<bool>)command.Options.First(o => o.Name == "parallel");

        var result = command.Parse("-appJsonPath test.json");
        var value = result.GetValueForOption(option);

        Assert.True(value);
    }

    [Fact]
    public void CompileCommand_MaxDegreeOfParallelismDefaultsTo4()
    {
        var command = CompileCommand.Create();
        var option = (Option<int>)command.Options.First(o => o.Name == "maxDegreeOfParallelism");

        var result = command.Parse("-appJsonPath test.json");
        var value = result.GetValueForOption(option);

        Assert.Equal(4, value);
    }

    [Fact]
    public void CompileCommand_GenerateReportLayoutDefaultsToFalse()
    {
        var command = CompileCommand.Create();
        var option = (Option<bool>)command.Options.First(o => o.Name == "generateReportLayout");

        var result = command.Parse("-appJsonPath test.json");
        var value = result.GetValueForOption(option);

        Assert.False(value);
    }

    [Fact]
    public void CompileCommand_ContinueBuildOnErrorDefaultsToTrue()
    {
        var command = CompileCommand.Create();
        var option = (Option<bool>)command.Options.First(o => o.Name == "continueBuildOnError");

        var result = command.Parse("-appJsonPath test.json");
        var value = result.GetValueForOption(option);

        Assert.True(value);
    }

    [Fact]
    public void CompileCommand_SuppressWarningsDefaultsToFalse()
    {
        var command = CompileCommand.Create();
        var option = (Option<bool>)command.Options.First(o => o.Name == "suppressWarnings");

        var result = command.Parse("-appJsonPath test.json");
        var value = result.GetValueForOption(option);

        Assert.False(value);
    }
}
