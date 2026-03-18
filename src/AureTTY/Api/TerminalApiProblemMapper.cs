using AureTTY.Contracts.Exceptions;
using AureTTY.Api.Models;
using AureTTY.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AureTTY.Api;

public static class TerminalApiProblemMapper
{
    public static ActionResult Map(ControllerBase controller, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(exception);

        var root = exception.GetBaseException();
        return root switch
        {
            TerminalSessionValidationException => controller.BadRequest(new ApiErrorResponse { Error = root.Message }),
            ArgumentException => controller.BadRequest(new ApiErrorResponse { Error = root.Message }),
            TerminalSessionNotFoundException => controller.NotFound(new ApiErrorResponse { Error = root.Message }),
            TerminalSessionForbiddenException => controller.StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse { Error = root.Message }),
            TerminalSessionConflictException => controller.Conflict(new ApiErrorResponse { Error = root.Message }),
            UnauthorizedAccessException => controller.Unauthorized(),
            _ => controller.Problem(
                title: "Terminal operation failed.",
                detail: root.Message,
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    public static IResult Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var root = exception.GetBaseException();
        return root switch
        {
            TerminalSessionValidationException => Results.Json(
                new ApiErrorResponse { Error = root.Message },
                AureTTYJsonSerializerContext.Default.ApiErrorResponse,
                statusCode: StatusCodes.Status400BadRequest),
            ArgumentException => Results.Json(
                new ApiErrorResponse { Error = root.Message },
                AureTTYJsonSerializerContext.Default.ApiErrorResponse,
                statusCode: StatusCodes.Status400BadRequest),
            TerminalSessionNotFoundException => Results.Json(
                new ApiErrorResponse { Error = root.Message },
                AureTTYJsonSerializerContext.Default.ApiErrorResponse,
                statusCode: StatusCodes.Status404NotFound),
            TerminalSessionForbiddenException => Results.Json(
                new ApiErrorResponse { Error = root.Message },
                AureTTYJsonSerializerContext.Default.ApiErrorResponse,
                statusCode: StatusCodes.Status403Forbidden),
            TerminalSessionConflictException => Results.Json(
                new ApiErrorResponse { Error = root.Message },
                AureTTYJsonSerializerContext.Default.ApiErrorResponse,
                statusCode: StatusCodes.Status409Conflict),
            UnauthorizedAccessException => Results.Unauthorized(),
            _ => Results.Json(
                new ApiProblemResponse
                {
                    Title = "Terminal operation failed.",
                    Detail = root.Message,
                    Status = StatusCodes.Status500InternalServerError
                },
                AureTTYJsonSerializerContext.Default.ApiProblemResponse,
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
