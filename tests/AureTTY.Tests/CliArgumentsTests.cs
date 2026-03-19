using System.Reflection;
using AureTTY.Cli;
using AureTTY.Contracts.Configuration;
using AureTTY.Protocol;
using AureTTY.Services;

namespace AureTTY.Tests;

public sealed class CliArgumentsTests
{
    [Fact]
    public void TryCreate_WhenSecretsAreMissing_ReturnsFalse()
    {
        var parseResult = Parse();

        var parsed = CliArguments.TryCreate(
            parseResult,
            allowMissingSecrets: false,
            out var arguments,
            out var error);

        Assert.False(parsed);
        Assert.Null(arguments);
        Assert.NotNull(error);
        Assert.Contains("--pipe-token", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_WhenOpenApiModeAllowsMissingSecrets_ParsesSuccessfully()
    {
        var parseResult = Parse();

        var parsed = CliArguments.TryCreate(
            parseResult,
            allowMissingSecrets: true,
            out var arguments,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(arguments);
        Assert.Equal(TerminalIpcDefaults.PipeName, arguments.PipeName);
        Assert.Equal(string.Empty, arguments.PipeToken);
        Assert.Equal(string.Empty, arguments.ApiKey);
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
            "super-secret",
            "--pipe-token",
            "pipe-secret");

        var parsed = CliArguments.TryCreate(
            parseResult,
            allowMissingSecrets: false,
            out var arguments,
            out var error);

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
        var parseResult = Parse(
            "--pipe-token",
            "pipe-secret",
            "--api-key",
            "api-secret",
            "--http-listen-url",
            "notaurl");

        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(parseResult.Errors, error => error.Message.Contains("Invalid --http-listen-url value", StringComparison.Ordinal));
    }

    [Fact]
    public void TryCreate_WhenTransportNormalizationFails_ReturnsFalse()
    {
        var parseResult = Parse("--transport", "grpc");

        var parsed = CliArguments.TryCreate(
            parseResult,
            allowMissingSecrets: false,
            out var arguments,
            out var error);

        Assert.False(parsed);
        Assert.Null(arguments);
        Assert.NotNull(error);
        Assert.Contains("Unsupported --transport value", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_WhenRuntimeLimitsInvalid_ReturnsFalse()
    {
        var parseResult = Parse(
            "--pipe-token",
            "pipe-secret",
            "--api-key",
            "api-secret",
            "--max-concurrent-sessions",
            "2",
            "--max-sessions-per-viewer",
            "3");

        var parsed = CliArguments.TryCreate(
            parseResult,
            allowMissingSecrets: false,
            out var arguments,
            out var error);

        Assert.False(parsed);
        Assert.Null(arguments);
        Assert.NotNull(error);
        Assert.Contains("MaxSessionsPerViewer", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryNormalizeTransports_WhenNullValues_ReturnsRequiredError()
    {
        var (parsed, normalized, error) = TryNormalizeTransports(values: null);

        Assert.False(parsed);
        Assert.Empty(normalized);
        Assert.Equal("At least one --transport value is required.", error);
    }

    [Fact]
    public void TryNormalizeTransports_WhenValuesContainOnlyWhitespace_ReturnsRequiredError()
    {
        var (parsed, normalized, error) = TryNormalizeTransports([" ", "\t"]);

        Assert.False(parsed);
        Assert.Empty(normalized);
        Assert.Equal("At least one --transport value is required.", error);
    }

    [Fact]
    public void Parse_WhenTransportEnvironmentVariableConfigured_UsesSplitDefaults()
    {
        var previous = Environment.GetEnvironmentVariable(CliArguments.TransportsEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(CliArguments.TransportsEnvironmentVariable, "http;pipe");

            var parseResult = Parse("--pipe-token", "pipe-secret", "--api-key", "api-secret");
            var parsed = CliArguments.TryCreate(
                parseResult,
                allowMissingSecrets: false,
                out var arguments,
                out var error);

            Assert.True(parsed);
            Assert.Null(error);
            Assert.NotNull(arguments);
            Assert.True(arguments.EnablePipeApi);
            Assert.True(arguments.EnableHttpApi);
        }
        finally
        {
            Environment.SetEnvironmentVariable(CliArguments.TransportsEnvironmentVariable, previous);
        }
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
            "api-key-value",
            "--max-concurrent-sessions",
            "64",
            "--max-sessions-per-viewer",
            "16",
            "--replay-buffer-capacity",
            "6000",
            "--max-pending-input-chunks",
            "16000",
            "--sse-subscription-buffer-capacity",
            "256",
            "--allow-api-key-query");

        var parsed = CliArguments.TryCreate(
            parseResult,
            allowMissingSecrets: false,
            out var arguments,
            out var error);

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
        Assert.Equal(
            new TerminalRuntimeLimits(64, 16, 6000, 16000),
            options.RuntimeLimits);
        Assert.Equal(256, options.SseSubscriptionBufferCapacity);
        Assert.True(options.AllowApiKeyQueryParameter);
    }

    private static System.CommandLine.ParseResult Parse(params string[] args)
    {
        var command = new System.CommandLine.RootCommand();
        CliOptions.AddTo(command);
        return command.Parse(args);
    }

    private static (bool Parsed, HashSet<string> Normalized, string? Error) TryNormalizeTransports(IEnumerable<string>? values)
    {
        var method = typeof(CliArguments).GetMethod("TryNormalizeTransports", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var args = new object?[] { values, null, null };
        var parsed = (bool)method!.Invoke(null, args)!;
        var normalized = Assert.IsType<HashSet<string>>(args[1]);
        var error = args[2] as string;
        return (parsed, normalized, error);
    }
}
