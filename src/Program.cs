using System.CommandLine;
using BCDev.Commands;

namespace BCDev;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("BC Dev CLI - Cross-platform tool for Business Central development operations");

        // Add compile command
        rootCommand.AddCommand(CompileCommand.Create());

        // Add publish command
        rootCommand.AddCommand(PublishCommand.Create());

        // Add test command
        rootCommand.AddCommand(TestCommand.Create());

        // Add symbols command
        rootCommand.AddCommand(SymbolsCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}
