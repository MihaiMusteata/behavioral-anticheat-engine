using System.Security.Claims;
using BehavioralAnticheatEngine.Application.Common.Exceptions;

namespace BehavioralAnticheatEngine.Api.Endpoints;

internal static class EndpointHelpers
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var userIdValue =
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            principal.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdValue, out var userId) ? userId : Guid.Empty;
    }

    public static async Task<IResult> ExecuteAsync<T>(
        Func<Task<T>> action,
        Func<T, IResult>? success = null)
    {
        try
        {
            var result = await action();
            return success?.Invoke(result) ?? Results.Ok(result);
        }
        catch (ApplicationValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ApplicationNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (ApplicationConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (ApplicationForbiddenException exception)
        {
            return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ApplicationUnauthorizedException)
        {
            return Results.Unauthorized();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}
