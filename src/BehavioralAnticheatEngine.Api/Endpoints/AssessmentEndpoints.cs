using System.Security.Claims;
using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Exams;
using BehavioralAnticheatEngine.Application.Exams.Contracts;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class AssessmentEndpoints
{
    public static RouteGroupBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/assessments")
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Admin, UserRoles.Proctor))
            .WithTags("Assessments");

        group.MapGet("/", (IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new ListAssessmentsQuery(), cancellationToken)));

        group.MapGet("/{assessmentId:guid}", (Guid assessmentId, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new GetAssessmentQuery(assessmentId), cancellationToken)));

        group.MapPost("/", (CreateAssessmentRequest request, ClaimsPrincipal principal, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(
                () => mediator.SendAsync(new CreateAssessmentCommand(principal.GetUserId(), request), cancellationToken),
                assessment => Results.Created($"/api/assessments/{assessment.Id}", assessment)));

        group.MapPut("/{assessmentId:guid}", (Guid assessmentId, UpdateAssessmentRequest request, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new UpdateAssessmentCommand(assessmentId, request), cancellationToken)));

        group.MapDelete("/{assessmentId:guid}", (Guid assessmentId, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new DeleteAssessmentCommand(assessmentId), cancellationToken)));

        group.MapPost("/{assessmentId:guid}/questions", (Guid assessmentId, UpsertQuestionRequest request, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(
                () => mediator.SendAsync(new AddQuestionCommand(assessmentId, request), cancellationToken),
                question => Results.Created($"/api/questions/{question.Id}", question)));

        routes.MapPut("/api/questions/{questionId:guid}", (Guid questionId, UpsertQuestionRequest request, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new UpdateQuestionCommand(questionId, request), cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Admin, UserRoles.Proctor))
            .WithTags("Assessments");

        routes.MapDelete("/api/questions/{questionId:guid}", (Guid questionId, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new DeleteQuestionCommand(questionId), cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Admin, UserRoles.Proctor))
            .WithTags("Assessments");

        return group;
    }
}
