using System.CommandLine;
using System.CommandLine.Parsing;
using AureTTY.Cli.Handlers;

namespace AureTTY.Cli;

public static class CliApp
{
    public static Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var handler = new RunServiceCommandHandler();
        var rootCommand = BuildRootCommand(handler);
        var parseResult = CommandLineParser.Parse(rootCommand, args, new ParserConfiguration());

        return parseResult.InvokeAsync();
    }

    private static RootCommand BuildRootCommand(RunServiceCommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var rootCommand = new RootCommand("AureTTY terminal service.");
        CliOptions.AddTo(rootCommand);
        rootCommand.SetAction((parseResult, cancellationToken) =>
            handler.ExecuteAsync(parseResult, cancellationToken));
        return rootCommand;
    }
}
