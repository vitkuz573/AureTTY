using AureTTY.Contracts.Exceptions;
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
            TerminalSessionValidationException => controller.BadRequest(new { error = root.Message }),
            ArgumentException => controller.BadRequest(new { error = root.Message }),
            TerminalSessionNotFoundException => controller.NotFound(new { error = root.Message }),
            TerminalSessionForbiddenException => controller.StatusCode(StatusCodes.Status403Forbidden, new { error = root.Message }),
            TerminalSessionConflictException => controller.Conflict(new { error = root.Message }),
            UnauthorizedAccessException => controller.Unauthorized(),
            _ => controller.Problem(
                title: "Terminal operation failed.",
                detail: root.Message,
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
