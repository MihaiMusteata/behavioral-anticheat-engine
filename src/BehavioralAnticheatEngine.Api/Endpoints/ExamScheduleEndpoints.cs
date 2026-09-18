using System.Security.Claims;
using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Exams;
using BehavioralAnticheatEngine.Application.Exams.Contracts;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class ExamScheduleEndpoints
{
    public static RouteGroupBuilder MapExamScheduleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/exams")
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Admin, UserRoles.Proctor))
            .WithTags("Exam schedules");

        group.MapGet("/", (IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new ListExamSchedulesQuery(), cancellationToken)));

        group.MapPost("/", (CreateExamScheduleRequest request, ClaimsPrincipal principal, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(
                () => mediator.SendAsync(new CreateExamScheduleCommand(principal.GetUserId(), request), cancellationToken),
                schedule => Results.Created($"/api/exams/{schedule.Id}", schedule)));

        group.MapPut("/{examScheduleId:guid}", (Guid examScheduleId, UpdateExamScheduleRequest request, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new UpdateExamScheduleCommand(examScheduleId, request), cancellationToken)));

        group.MapDelete("/{examScheduleId:guid}", (Guid examScheduleId, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new DeleteExamScheduleCommand(examScheduleId), cancellationToken)));

        group.MapPost("/{examScheduleId:guid}/start", (Guid examScheduleId, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new StartScheduledExamCommand(examScheduleId), cancellationToken)));

        group.MapPost("/{examScheduleId:guid}/stop", (Guid examScheduleId, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new StopScheduledExamCommand(examScheduleId), cancellationToken)));

        return group;
    }
}
