using System.CommandLine;
using System.CommandLine.Parsing;
using BCDev.Commands;
using Xunit;

namespace BCDev.Tests.Unit;

public class SymbolsCommandFlagTests
{
    [Fact]
    public void SymbolsCommand_HasFromNuGetOption()
    {
        var command = SymbolsCommand.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "fromNuGet");

        Assert.NotNull(option);
        Assert.False(((Option<bool>)option).Parse("-fromNuGet false").GetValueForOption((Option<bool>)option));
    }

    [Fact]
    public void SymbolsCommand_DoesNotHaveFromServerOption()
    {
        var command = SymbolsCommand.Create();
        var option = command.Options.FirstOrDefault(o => o.Name == "fromServer");

        Assert.Null(option);
    }

    [Fact]
    public void SymbolsCommand_FromNuGetDefaultsToFalse()
    {
        var command = SymbolsCommand.Create();
        var option = (Option<bool>)command.Options.First(o => o.Name == "fromNuGet");

        // Parse with no -fromNuGet flag
        var result = command.Parse("-appJsonPath test.json");
        var value = result.GetValueForOption(option);

        Assert.False(value);
    }
}
