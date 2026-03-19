using AureTTY.Contracts.DTOs;
using AureTTY.Contracts.Enums;
using AureTTY.Protocol;

namespace AureTTY.Tests;

public sealed class ProtocolPayloadsTests
{
    [Fact]
    public void ProtocolConstants_AreStable()
    {
        Assert.Equal("auretty-terminal", TerminalIpcDefaults.PipeName);
        Assert.Equal("auretty-terminal-token", TerminalIpcDefaults.PipeToken);
        Assert.NotEqual(TerminalIpcDefaults.PipeName, TerminalIpcDefaults.PipeToken);
        Assert.Equal("hello", TerminalIpcMethods.Hello);
        Assert.Equal("request", TerminalIpcMessageTypes.Request);
    }

    [Fact]
    public void PayloadRecords_PreserveProvidedValues()
    {
        var sessionId = "session-42";
        var viewerId = "viewer-42";

        var startRequest = new TerminalIpcStartRequest(viewerId, new TerminalSessionStartRequest(sessionId, Shell.Pwsh));
        var resumeRequest = new TerminalIpcResumeRequest(viewerId, new TerminalSessionResumeRequest(sessionId));
        var inputRequest = new TerminalIpcInputRequest(viewerId, new TerminalSessionInputRequest(sessionId, "echo test", 1));
        var diagnosticsRequest = new TerminalIpcInputDiagnosticsRequest(viewerId, sessionId);
        var resizeRequest = new TerminalIpcResizeRequest(viewerId, new TerminalSessionResizeRequest(sessionId, 120, 30));
        var signalRequest = new TerminalIpcSignalRequest(viewerId, sessionId, TerminalSessionSignal.Interrupt);
        var closeRequest = new TerminalIpcCloseRequest(viewerId, sessionId);
        var closeViewerRequest = new TerminalIpcCloseViewerSessionsRequest(viewerId);
        var sessionEvent = new TerminalIpcSessionEvent(viewerId, new TerminalSessionEvent(sessionId, TerminalSessionEventType.Output));
        var helloPayload = new TerminalIpcHelloPayload("token");
        var ack = new TerminalIpcAck();

        Assert.Equal(viewerId, startRequest.ViewerId);
        Assert.Equal(viewerId, resumeRequest.ViewerId);
        Assert.Equal(viewerId, inputRequest.ViewerId);
        Assert.Equal(sessionId, diagnosticsRequest.SessionId);
        Assert.Equal(120, resizeRequest.Request.Columns);
        Assert.Equal(TerminalSessionSignal.Interrupt, signalRequest.Signal);
        Assert.Equal(sessionId, closeRequest.SessionId);
        Assert.Equal(viewerId, closeViewerRequest.ViewerId);
        Assert.Equal(sessionId, sessionEvent.Event.SessionId);
        Assert.Equal("token", helloPayload.Token);
        Assert.Equal(1, helloPayload.ProtocolVersion);
        Assert.True(ack.Success);
    }
}
