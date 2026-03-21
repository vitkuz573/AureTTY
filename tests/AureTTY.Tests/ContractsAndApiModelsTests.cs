using AureTTY.Api.Models;
using AureTTY.Contracts.Enums;
using AureTTY.Contracts.Exceptions;

namespace AureTTY.Tests;

public sealed class ContractsAndApiModelsTests
{
    [Fact]
    public void ContractExceptions_ExposeProvidedMessage()
    {
        var conflict = new TerminalSessionConflictException("conflict");
        var notFound = new TerminalSessionNotFoundException("missing");
        var validation = new TerminalSessionValidationException("invalid");

        Assert.Equal("conflict", conflict.Message);
        Assert.Equal("missing", notFound.Message);
        Assert.Equal("invalid", validation.Message);
    }

    [Fact]
    public void ApiModels_PreserveAssignedValues()
    {
        var signalRequest = new CreateTerminalSignalRequest
        {
            Signal = TerminalSessionSignal.Terminate
        };

        var attachRequest = new AttachTerminalSessionRequest
        {
            LastReceivedSequenceNumber = 10,
            Columns = 120,
            Rows = 30
        };

        var resizeRequest = new UpdateTerminalSizeRequest
        {
            Columns = 140,
            Rows = 40
        };

        var healthResponse = new TerminalHealthResponse
        {
            Status = "ok",
            ApiVersion = "v1",
            Transports = ["http", "ws", "pipe"],
            WebSocketHelloTimeoutSeconds = 5,
            SessionIdleTimeoutSeconds = 900,
            SessionHardLifetimeSeconds = 14400
        };

        Assert.Equal(TerminalSessionSignal.Terminate, signalRequest.Signal);
        Assert.Equal(10, attachRequest.LastReceivedSequenceNumber);
        Assert.Equal(120, attachRequest.Columns);
        Assert.Equal(30, attachRequest.Rows);
        Assert.Equal(140, resizeRequest.Columns);
        Assert.Equal(40, resizeRequest.Rows);
        Assert.Equal("ok", healthResponse.Status);
        Assert.Equal("v1", healthResponse.ApiVersion);
        Assert.Equal(["http", "ws", "pipe"], healthResponse.Transports);
        Assert.Equal(5, healthResponse.WebSocketHelloTimeoutSeconds);
        Assert.Equal(900, healthResponse.SessionIdleTimeoutSeconds);
        Assert.Equal(14400, healthResponse.SessionHardLifetimeSeconds);
    }
}
