using System.Security.Claims;
using BehavioralAnticheatEngine.Application.Auth;
using BehavioralAnticheatEngine.Application.Auth.Contracts;
using BehavioralAnticheatEngine.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/signup", SignUpAsync)
            .AllowAnonymous()
            .WithName("SignUp");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login");

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshToken");

        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return group;
    }

    private static async Task<IResult> SignUpAsync(
        SignUpRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAuthAsync(
            () => authService.SignUpAsync(request, cancellationToken),
            response => Results.Created($"/api/auth/users/{response.UserId}", response));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAuthAsync(
            () => authService.LoginAsync(request, cancellationToken),
            response => Results.Ok(response));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        return await ExecuteAuthAsync(
            () => authService.RefreshAsync(request, cancellationToken),
            response => Results.Ok(response));
    }

    private static Ok<UserProfileResponse> GetCurrentUser(ClaimsPrincipal principal)
    {
        var userIdValue =
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            principal.FindFirst("sub")?.Value ??
            string.Empty;
        var email =
            principal.FindFirst(ClaimTypes.Email)?.Value ??
            principal.FindFirst("email")?.Value ??
            string.Empty;
        var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        return TypedResults.Ok(new UserProfileResponse(Guid.Parse(userIdValue), email, role));
    }

    private static async Task<IResult> ExecuteAuthAsync(
        Func<Task<AuthResponse>> action,
        Func<AuthResponse, IResult> success)
    {
        try
        {
            var response = await action();

            return success(response);
        }
        catch (ApplicationValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ApplicationConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (ApplicationUnauthorizedException)
        {
            return Results.Unauthorized();
        }
    }
}
