using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Protocol;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

var pipeNameOption = new Option<string?>("--pipe-name")
{
    Description = "Pipe name used by AureTTY."
};
pipeNameOption.DefaultValueFactory = _ => TerminalIpcDefaults.PipeName;

var pipeTokenOption = new Option<string?>("--pipe-token")
{
    Description = "Pipe auth token."
};
pipeTokenOption.DefaultValueFactory = _ => TerminalIpcDefaults.PipeToken;

var viewerIdOption = new Option<string?>("--viewer-id")
{
    Description = "Viewer identifier."
};
viewerIdOption.DefaultValueFactory = _ => $"demo-pipe-viewer-{Guid.NewGuid():N}";

var sessionIdOption = new Option<string?>("--session-id")
{
    Description = "Session identifier."
};
sessionIdOption.DefaultValueFactory = _ => $"demo-pipe-session-{Guid.NewGuid():N}";

var shellOption = new Option<string?>("--shell")
{
    Description = "Shell to request (bash|pwsh|powershell|cmd)."
};
shellOption.DefaultValueFactory = _ => "bash";

var connectTimeoutOption = new Option<int>("--connect-timeout-seconds")
{
    Description = "Pipe connect/read timeout in seconds."
};
connectTimeoutOption.DefaultValueFactory = _ => 10;

var rootCommand = new RootCommand("AureTTY pipe transport demo client.");
rootCommand.Add(pipeNameOption);
rootCommand.Add(pipeTokenOption);
rootCommand.Add(viewerIdOption);
rootCommand.Add(sessionIdOption);
rootCommand.Add(shellOption);
rootCommand.Add(connectTimeoutOption);

rootCommand.SetAction((parseResult, cancellationToken) => ExecuteAsync(
    parseResult,
    cancellationToken,
    pipeNameOption,
    pipeTokenOption,
    viewerIdOption,
    sessionIdOption,
    shellOption,
    connectTimeoutOption,
    jsonOptions));

return await rootCommand.Parse(args).InvokeAsync();

static async Task<int> ExecuteAsync(
    ParseResult parseResult,
    CancellationToken cancellationToken,
    Option<string?> pipeNameOption,
    Option<string?> pipeTokenOption,
    Option<string?> viewerIdOption,
    Option<string?> sessionIdOption,
    Option<string?> shellOption,
    Option<int> connectTimeoutOption,
    JsonSerializerOptions jsonOptions)
{
    ArgumentNullException.ThrowIfNull(parseResult);
    ArgumentNullException.ThrowIfNull(pipeNameOption);
    ArgumentNullException.ThrowIfNull(pipeTokenOption);
    ArgumentNullException.ThrowIfNull(viewerIdOption);
    ArgumentNullException.ThrowIfNull(sessionIdOption);
    ArgumentNullException.ThrowIfNull(shellOption);
    ArgumentNullException.ThrowIfNull(connectTimeoutOption);
    ArgumentNullException.ThrowIfNull(jsonOptions);

    var pipeName = parseResult.GetValue(pipeNameOption);
    var pipeToken = parseResult.GetValue(pipeTokenOption);
    var viewerId = parseResult.GetValue(viewerIdOption);
    var sessionId = parseResult.GetValue(sessionIdOption);
    var shellName = parseResult.GetValue(shellOption);
    var connectTimeoutSeconds = parseResult.GetValue(connectTimeoutOption);

    if (string.IsNullOrWhiteSpace(pipeName))
    {
        Console.Error.WriteLine("--pipe-name is required.");
        return 2;
    }

    if (string.IsNullOrWhiteSpace(pipeToken))
    {
        Console.Error.WriteLine("--pipe-token is required.");
        return 2;
    }

    if (string.IsNullOrWhiteSpace(viewerId))
    {
        Console.Error.WriteLine("--viewer-id is required.");
        return 2;
    }

    if (string.IsNullOrWhiteSpace(sessionId))
    {
        Console.Error.WriteLine("--session-id is required.");
        return 2;
    }

    if (connectTimeoutSeconds <= 0)
    {
        Console.Error.WriteLine("--connect-timeout-seconds must be greater than zero.");
        return 2;
    }

    if (!TryParseShell(shellName, out var shell))
    {
        Console.Error.WriteLine($"Unsupported --shell value '{shellName}'. Use bash|pwsh|powershell|cmd.");
        return 2;
    }

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(connectTimeoutSeconds));

    using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    Console.WriteLine($"[pipe-demo] Connecting to pipe '{pipeName}'...");
    await pipe.ConnectAsync(timeout.Token);

    using var reader = CreateReader(pipe);
    await using var writer = CreateWriter(pipe);

    await CompleteHandshakeAsync(reader, writer, pipeToken, jsonOptions, timeout.Token);

    var pingAck = await SendRequestAsync<TerminalIpcAck, TerminalIpcAck>(
        reader,
        writer,
        TerminalIpcMethods.Ping,
        new TerminalIpcAck(),
        jsonOptions,
        timeout.Token);
    Console.WriteLine($"[pipe-demo] Ping response success={pingAck.Success}.");

    var startedSession = await SendRequestAsync<TerminalIpcStartRequest, TerminalSessionHandle>(
        reader,
        writer,
        TerminalIpcMethods.Start,
        new TerminalIpcStartRequest(viewerId, new TerminalSessionStartRequest(sessionId, shell)),
        jsonOptions,
        timeout.Token);
    Console.WriteLine($"[pipe-demo] Started session '{startedSession.SessionId}' state={startedSession.State} pid={startedSession.ProcessId?.ToString() ?? "n/a"}.");

    _ = await SendRequestAsync<TerminalIpcInputRequest, TerminalIpcAck>(
        reader,
        writer,
        TerminalIpcMethods.SendInput,
        new TerminalIpcInputRequest(
            viewerId,
            new TerminalSessionInputRequest(startedSession.SessionId, BuildDemoInput(shell), 1)),
        jsonOptions,
        timeout.Token);

    var diagnostics = await SendRequestAsync<TerminalIpcInputDiagnosticsRequest, TerminalSessionInputDiagnostics>(
        reader,
        writer,
        TerminalIpcMethods.GetInputDiagnostics,
        new TerminalIpcInputDiagnosticsRequest(viewerId, startedSession.SessionId),
        jsonOptions,
        timeout.Token);
    Console.WriteLine($"[pipe-demo] Diagnostics: viewer={diagnostics.ViewerId}, session={diagnostics.SessionId}, nextExpected={diagnostics.NextExpectedSequence}, lastAccepted={diagnostics.LastAcceptedSequence}.");

    _ = await SendRequestAsync<TerminalIpcCloseViewerSessionsRequest, TerminalIpcAck>(
        reader,
        writer,
        TerminalIpcMethods.CloseViewerSessions,
        new TerminalIpcCloseViewerSessionsRequest(viewerId),
        jsonOptions,
        timeout.Token);

    Console.WriteLine("[pipe-demo] Completed successfully.");
    return 0;
}

static StreamReader CreateReader(Stream stream)
{
    return new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, leaveOpen: true);
}

static StreamWriter CreateWriter(Stream stream)
{
    return new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
    {
        AutoFlush = true,
        NewLine = "\n"
    };
}

static async Task CompleteHandshakeAsync(
    StreamReader reader,
    StreamWriter writer,
    string token,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    await WriteMessageAsync(writer, new TerminalIpcMessage
    {
        Type = TerminalIpcMessageTypes.Hello,
        Method = TerminalIpcMethods.Hello,
        Payload = JsonSerializer.SerializeToElement(new TerminalIpcHelloPayload(token), jsonOptions)
    }, jsonOptions, cancellationToken);

    var helloMessage = await ReadMessageAsync(reader, jsonOptions, cancellationToken);
    if (!string.Equals(helloMessage.Type, TerminalIpcMessageTypes.Hello, StringComparison.Ordinal)
        || !string.Equals(helloMessage.Method, TerminalIpcMethods.Hello, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Unexpected handshake message from AureTTY pipe server.");
    }

    var helloPayload = DeserializePayload<TerminalIpcHelloPayload>(helloMessage, jsonOptions);
    if (!string.Equals(helloPayload.Token, token, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("AureTTY pipe handshake token mismatch.");
    }

    Console.WriteLine("[pipe-demo] Handshake completed.");
}

static async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
    StreamReader reader,
    StreamWriter writer,
    string method,
    TRequest payload,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    var requestId = Guid.NewGuid().ToString("N");

    await WriteMessageAsync(writer, new TerminalIpcMessage
    {
        Type = TerminalIpcMessageTypes.Request,
        Id = requestId,
        Method = method,
        Payload = JsonSerializer.SerializeToElement(payload, jsonOptions)
    }, jsonOptions, cancellationToken);

    while (true)
    {
        var message = await ReadMessageAsync(reader, jsonOptions, cancellationToken);

        if (string.Equals(message.Type, TerminalIpcMessageTypes.Event, StringComparison.Ordinal))
        {
            var terminalEventPayload = DeserializePayload<TerminalIpcSessionEvent>(message, jsonOptions);
            Console.WriteLine($"[pipe-demo] Event {terminalEventPayload.Event.EventType}: session={terminalEventPayload.Event.SessionId}, text='{terminalEventPayload.Event.Text ?? string.Empty}'.");
            continue;
        }

        if (!string.Equals(message.Id, requestId, StringComparison.Ordinal))
        {
            continue;
        }

        if (string.Equals(message.Type, TerminalIpcMessageTypes.Error, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Pipe request '{method}' failed: {message.Error ?? "unknown error"}.");
        }

        if (!string.Equals(message.Type, TerminalIpcMessageTypes.Response, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Pipe request '{method}' returned unexpected message type '{message.Type}'.");
        }

        return DeserializePayload<TResponse>(message, jsonOptions);
    }
}

static T DeserializePayload<T>(TerminalIpcMessage message, JsonSerializerOptions jsonOptions)
{
    if (message.Payload is not JsonElement payloadElement)
    {
        throw new InvalidOperationException("IPC message payload is missing.");
    }

    var payload = payloadElement.Deserialize<T>(jsonOptions);
    if (payload is null)
    {
        throw new InvalidOperationException($"IPC payload deserialization failed for type '{typeof(T).Name}'.");
    }

    return payload;
}

static async Task<TerminalIpcMessage> ReadMessageAsync(
    StreamReader reader,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    var line = await reader.ReadLineAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(line))
    {
        throw new InvalidOperationException("Received empty IPC frame from AureTTY pipe server.");
    }

    var message = JsonSerializer.Deserialize<TerminalIpcMessage>(line, jsonOptions);
    if (message is null)
    {
        throw new InvalidOperationException("Unable to deserialize IPC message.");
    }

    return message;
}

static async Task WriteMessageAsync(
    StreamWriter writer,
    TerminalIpcMessage message,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    var line = JsonSerializer.Serialize(message, jsonOptions);
    await writer.WriteLineAsync(line);
    await writer.FlushAsync(cancellationToken);
}

static bool TryParseShell(string? shellName, out Shell shell)
{
    shell = Shell.Bash;
    if (string.IsNullOrWhiteSpace(shellName))
    {
        return false;
    }

    return shellName.Trim().ToLowerInvariant() switch
    {
        "bash" => SetShell(Shell.Bash, out shell),
        "pwsh" => SetShell(Shell.Pwsh, out shell),
        "powershell" => SetShell(Shell.PowerShell, out shell),
        "cmd" => SetShell(Shell.Cmd, out shell),
        _ => false
    };
}

static bool SetShell(Shell value, out Shell shell)
{
    shell = value;
    return true;
}

static string BuildDemoInput(Shell shell)
{
    return shell switch
    {
        Shell.Cmd => "echo demo-pipe && ver\r\n",
        Shell.PowerShell or Shell.Pwsh => "Write-Output demo-pipe\r\n$PSVersionTable.PSEdition\r\n",
        Shell.Bash => "echo demo-pipe && uname -s\n",
        _ => throw new InvalidOperationException($"Unsupported shell '{shell}'.")
    };
}
