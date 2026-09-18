using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Exams;
using BehavioralAnticheatEngine.Application.Exams.Contracts;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class GuestEndpoints
{
    public static RouteGroupBuilder MapGuestEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/guest")
            .WithTags("Guest exam participation");

        group.MapPost("/join", (JoinExamGuestRequest request, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new JoinExamAsGuestCommand(request), cancellationToken)))
            .AllowAnonymous()
            .WithName("JoinExamAsGuest");

        return group;
    }
}
