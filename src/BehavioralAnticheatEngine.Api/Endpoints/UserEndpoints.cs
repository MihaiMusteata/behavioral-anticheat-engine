using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Users;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users")
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Admin, UserRoles.Proctor))
            .WithTags("Users");

        group.MapGet("/students", (IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new ListStudentsQuery(), cancellationToken)));

        return group;
    }
}
