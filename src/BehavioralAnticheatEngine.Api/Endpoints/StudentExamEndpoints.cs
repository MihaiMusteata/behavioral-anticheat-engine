using System.Security.Claims;
using System.Text.Json;
using BehavioralAnticheatEngine.Application.Common.Contracts;
using BehavioralAnticheatEngine.Application.Common.Exceptions;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Exams;
using BehavioralAnticheatEngine.Application.Exams.Contracts;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class StudentExamEndpoints
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapStudentExamEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/student")
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Student))
            .WithTags("Student exam");

        group.MapPost("/exams/{examScheduleId:guid}/start", (Guid examScheduleId, ClaimsPrincipal principal, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new StartExamSessionCommand(examScheduleId, principal.GetUserId()), cancellationToken)));

        group.MapPut("/sessions/{sessionId:guid}/answers", (Guid sessionId, SecureActionEnvelope envelope, ClaimsPrincipal principal, ISecureSessionActionUnwrapper unwrapper, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(async () =>
            {
                var request = await UnwrapAsync<SaveAnswerRequest>(sessionId, envelope, principal.GetUserId(), unwrapper, cancellationToken);
                return await mediator.SendAsync(new SaveAnswerCommand(sessionId, principal.GetUserId(), request), cancellationToken);
            }));

        group.MapPost("/sessions/{sessionId:guid}/submit", (Guid sessionId, SecureActionEnvelope envelope, ClaimsPrincipal principal, ISecureSessionActionUnwrapper unwrapper, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(async () =>
            {
                await RequireValidEnvelopeAsync(sessionId, envelope, principal.GetUserId(), unwrapper, cancellationToken);
                return await mediator.SendAsync(new SubmitExamCommand(sessionId, principal.GetUserId()), cancellationToken);
            }));

        group.MapPut("/sessions/{sessionId:guid}/self-report", (Guid sessionId, SecureActionEnvelope envelope, ClaimsPrincipal principal, ISecureSessionActionUnwrapper unwrapper, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(async () =>
            {
                var request = await UnwrapAsync<SelfReportCheatingRequest>(sessionId, envelope, principal.GetUserId(), unwrapper, cancellationToken);
                return await mediator.SendAsync(new SelfReportCheatingCommand(sessionId, principal.GetUserId(), request), cancellationToken);
            }));

        return group;
    }

    private static async Task<string> RequireValidEnvelopeAsync(
        Guid sessionId,
        SecureActionEnvelope envelope,
        Guid studentId,
        ISecureSessionActionUnwrapper unwrapper,
        CancellationToken cancellationToken)
    {
        if (envelope.SessionId != sessionId)
        {
            throw new ApplicationValidationException("Request envelope does not match the session.");
        }

        return await unwrapper.UnwrapAsync(studentId, envelope, cancellationToken);
    }

    private static async Task<T> UnwrapAsync<T>(
        Guid sessionId,
        SecureActionEnvelope envelope,
        Guid studentId,
        ISecureSessionActionUnwrapper unwrapper,
        CancellationToken cancellationToken)
    {
        var payloadJson = await RequireValidEnvelopeAsync(sessionId, envelope, studentId, unwrapper, cancellationToken);
        return JsonSerializer.Deserialize<T>(payloadJson, PayloadJsonOptions) ??
            throw new ApplicationValidationException("Request payload was empty.");
    }
}
