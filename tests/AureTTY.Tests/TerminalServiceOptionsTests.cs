using AureTTY.Protocol;
using AureTTY.Services;

namespace AureTTY.Tests;

public sealed class TerminalServiceOptionsTests
{
    [Fact]
    public void TryParse_WhenAllRequiredArgumentsAreProvided_ReturnsOptions()
    {
        var args = new[]
        {
            "--pipe-name",
            "auretty-pipe",
            "--pipe-token",
            "token-value"
        };

        var parsed = TerminalServiceOptions.TryParse(args, out var options, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("auretty-pipe", options.PipeName);
        Assert.Equal("token-value", options.PipeToken);
    }

    [Fact]
    public void TryParse_WhenPipeNameIsMissing_UsesDefaultPipeName()
    {
        var args = new[]
        {
            "--pipe-token",
            "token-value"
        };

        var parsed = TerminalServiceOptions.TryParse(args, out var options, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(TerminalIpcDefaults.PipeName, options.PipeName);
        Assert.Equal("token-value", options.PipeToken);
    }

    [Fact]
    public void TryParse_WhenPipeTokenIsMissing_UsesDefaultPipeToken()
    {
        var args = new[]
        {
            "--pipe-name",
            "auretty-pipe"
        };

        var parsed = TerminalServiceOptions.TryParse(args, out var options, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("auretty-pipe", options.PipeName);
        Assert.Equal(TerminalIpcDefaults.PipeToken, options.PipeToken);
    }

    [Fact]
    public void TryParse_WhenArgumentsAreMissing_UsesDefaults()
    {
        var parsed = TerminalServiceOptions.TryParse([], out var options, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(TerminalIpcDefaults.PipeName, options.PipeName);
        Assert.Equal(TerminalIpcDefaults.PipeToken, options.PipeToken);
    }
}
