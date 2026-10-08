using System.Security.Claims;
using BehavioralAnticheatEngine.Application.Behavior;
using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class BehavioralEventEndpoints
{
    public static RouteGroupBuilder MapBehavioralEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/behavior")
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Student))
            .WithTags("Behavioral ingest");

        group.MapPost("/events", (BehavioralEventBatchRequest request, ClaimsPrincipal principal, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(
                () => mediator.SendAsync(new IngestBehavioralEventsCommand(principal.GetUserId(), request), cancellationToken),
                result => result.AcceptedCount == 0 && result.RejectedCount > 0
                    ? Results.BadRequest(result)
                    : Results.Ok(result)));

        return group;
    }
}
