using AureTTY.Cli;
using AureTTY.Protocol;
using AureTTY.Services;

namespace AureTTY.Tests;

public sealed class CliArgumentsTests
{
    [Fact]
    public void TryCreate_WhenArgumentsAreMissing_UsesDefaults()
    {
        var parseResult = Parse();

        var parsed = CliArguments.TryCreate(parseResult, out var arguments, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(arguments);
        Assert.Equal(TerminalIpcDefaults.PipeName, arguments.PipeName);
        Assert.Equal(TerminalIpcDefaults.PipeToken, arguments.PipeToken);
        Assert.True(arguments.EnablePipeApi);
        Assert.True(arguments.EnableHttpApi);
        Assert.Equal(TerminalServiceOptions.DefaultHttpListenUrl, arguments.HttpListenUrl);
        Assert.Equal(TerminalIpcDefaults.PipeToken, arguments.ApiKey);
    }

    [Fact]
    public void TryCreate_WhenHttpArgumentsAreProvided_UsesExplicitValues()
    {
        var parseResult = Parse(
            "--transport",
            "http",
            "--http-listen-url",
            "http://127.0.0.1:18888",
            "--api-key",
            "super-secret");

        var parsed = CliArguments.TryCreate(parseResult, out var arguments, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(arguments);
        Assert.False(arguments.EnablePipeApi);
        Assert.True(arguments.EnableHttpApi);
        Assert.Equal("http://127.0.0.1:18888", arguments.HttpListenUrl);
        Assert.Equal("super-secret", arguments.ApiKey);
    }

    [Fact]
    public void Parse_WhenTransportIsInvalid_ReturnsError()
    {
        var parseResult = Parse("--transport", "grpc");

        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(parseResult.Errors, error => error.Message.Contains("Unsupported --transport value", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_WhenHttpListenUrlIsInvalid_ReturnsError()
    {
        var parseResult = Parse("--http-listen-url", "notaurl");

        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(parseResult.Errors, error => error.Message.Contains("Invalid --http-listen-url value", StringComparison.Ordinal));
    }

    [Fact]
    public void ToTerminalServiceOptions_WhenArgumentsAreValid_MapsAllProperties()
    {
        var parseResult = Parse(
            "--pipe-name",
            "auretty-pipe",
            "--pipe-token",
            "token-value",
            "--transport",
            "pipe",
            "--transport",
            "http",
            "--http-listen-url",
            "http://127.0.0.1:17851",
            "--api-key",
            "api-key-value");

        var parsed = CliArguments.TryCreate(parseResult, out var arguments, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(arguments);

        var options = arguments.ToTerminalServiceOptions();

        Assert.Equal("auretty-pipe", options.PipeName);
        Assert.Equal("token-value", options.PipeToken);
        Assert.True(options.EnablePipeApi);
        Assert.True(options.EnableHttpApi);
        Assert.Equal("http://127.0.0.1:17851", options.HttpListenUrl);
        Assert.Equal("api-key-value", options.ApiKey);
    }

    private static System.CommandLine.ParseResult Parse(params string[] args)
    {
        var command = new System.CommandLine.RootCommand();
        CliOptions.AddTo(command);
        return command.Parse(args);
    }
}
