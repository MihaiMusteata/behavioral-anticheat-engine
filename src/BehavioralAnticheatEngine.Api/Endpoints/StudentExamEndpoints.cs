using System.Security.Claims;
using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Exams;
using BehavioralAnticheatEngine.Application.Exams.Contracts;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class StudentExamEndpoints
{
    public static RouteGroupBuilder MapStudentExamEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/student")
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Student))
            .WithTags("Student exam");

        group.MapPost("/exams/{examScheduleId:guid}/start", (Guid examScheduleId, ClaimsPrincipal principal, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new StartExamSessionCommand(examScheduleId, principal.GetUserId()), cancellationToken)));

        group.MapPut("/sessions/{sessionId:guid}/answers", (Guid sessionId, SaveAnswerRequest request, ClaimsPrincipal principal, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new SaveAnswerCommand(sessionId, principal.GetUserId(), request), cancellationToken)));

        group.MapPost("/sessions/{sessionId:guid}/submit", (Guid sessionId, ClaimsPrincipal principal, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new SubmitExamCommand(sessionId, principal.GetUserId()), cancellationToken)));

        group.MapPut("/sessions/{sessionId:guid}/self-report", (Guid sessionId, SelfReportCheatingRequest request, ClaimsPrincipal principal, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new SelfReportCheatingCommand(sessionId, principal.GetUserId(), request), cancellationToken)));

        return group;
    }
}
