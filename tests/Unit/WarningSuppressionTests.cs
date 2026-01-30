using BCDev.Services;
using Xunit;

namespace BCDev.Tests.Unit;

public class WarningSuppressionTests
{
    [Fact]
    public void FilterWarningsFromOutput_RemovesWarningLines()
    {
        var input = "src/app.al(10,5): warning AL0432: Method 'Foo' is marked for removal.\n" +
                    "src/app.al(15,1): error AL0118: 'Bar' is not a valid identifier\n" +
                    "src/other.al(20,3): warning AL0433: Field 'Baz' is obsolete.\n" +
                    "Compilation finished.";

        var filtered = CompilerService.FilterWarningsFromOutput(input);

        Assert.DoesNotContain(": warning ", filtered);
        Assert.Contains(": error ", filtered);
        Assert.Contains("Compilation finished.", filtered);
    }

    [Fact]
    public void FilterWarningsFromOutput_HandlesMultipleWarningTypes()
    {
        var input = "file.al(1,1): warning AL0432: Deprecation warning\n" +
                    "file.al(2,1): warning AL0433: Another warning\n" +
                    "file.al(3,1): error AL0001: An error\n" +
                    "file.al(4,1): warning AL1234: Yet another warning";

        var filtered = CompilerService.FilterWarningsFromOutput(input);
        var lines = filtered.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
        Assert.Contains("error AL0001", lines[0]);
    }

    [Fact]
    public void FilterWarningsFromOutput_PreservesEmptyAndNullInput()
    {
        Assert.Equal("", CompilerService.FilterWarningsFromOutput(""));
        Assert.Null(CompilerService.FilterWarningsFromOutput(null!));
    }

    [Fact]
    public void FilterWarningsFromOutput_PreservesNonWarningLines()
    {
        var input = "Microsoft (R) AL Compiler version 14.0.12345.0\n" +
                    "Copyright (C) Microsoft Corporation. All rights reserved.\n" +
                    "\n" +
                    "Compilation started...\n" +
                    "file.al(1,1): error AL0001: Error message\n" +
                    "Compilation failed.";

        var filtered = CompilerService.FilterWarningsFromOutput(input);

        Assert.Equal(input, filtered);
    }

    [Fact]
    public void FilterWarningsFromOutput_HandlesWindowsLineEndings()
    {
        var input = "file.al(1,1): warning AL0432: A warning\r\n" +
                    "file.al(2,1): error AL0001: An error\r\n" +
                    "Done.";

        var filtered = CompilerService.FilterWarningsFromOutput(input);

        Assert.DoesNotContain(": warning ", filtered);
        Assert.Contains(": error ", filtered);
        Assert.Contains("Done.", filtered);
    }
}
