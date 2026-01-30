using System.CommandLine;
using System.CommandLine.Parsing;
using BCDev.Commands;
using Xunit;

namespace BCDev.Tests.Unit;

public class SymbolsCommandFlagTests
{
    [Fact]
    public void SymbolsCommand_HasFromServerOption()
    {
        var command = SymbolsCommand.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "fromServer");

        Assert.NotNull(option);
    }

    [Fact]
    public void SymbolsCommand_DoesNotHaveFromNuGetOption()
    {
        var command = SymbolsCommand.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "fromNuGet");

        Assert.Null(option);
    }

    [Fact]
    public void SymbolsCommand_FromServerDefaultsToFalse()
    {
        var command = SymbolsCommand.Create();
        var option = (Option<bool>)command.Options.First(o => o.Name == "fromServer");

        // Parse with no -fromServer flag (NuGet is default)
        var result = command.Parse("-appJsonPath test.json");
        var value = result.GetValueForOption(option);

        Assert.False(value);
    }
}
