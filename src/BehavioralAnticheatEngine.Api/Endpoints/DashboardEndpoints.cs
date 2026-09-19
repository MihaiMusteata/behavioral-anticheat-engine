using System.Text;
using System.Text.Json;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Dashboard;
using BehavioralAnticheatEngine.Application.Exams;
using BehavioralAnticheatEngine.Application.Exams.Contracts;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Api.Endpoints;

public static class DashboardEndpoints
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dashboard")
            .RequireAuthorization(policy => policy.RequireRole(UserRoles.Admin, UserRoles.Proctor))
            .WithTags("Dashboard");

        group.MapGet("/exams/{examScheduleId:guid}", (Guid examScheduleId, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new GetExamDashboardQuery(examScheduleId), cancellationToken)));

        group.MapGet("/sessions/{sessionId:guid}/timeline", (Guid sessionId, Guid? questionId, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new GetSessionTimelineQuery(sessionId, questionId), cancellationToken)));

        group.MapGet("/sessions/{sessionId:guid}/timeline/export", (Guid sessionId, Guid? questionId, string? eventType, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(
                () => mediator.SendAsync(new ExportSessionTimelineQuery(sessionId, questionId, eventType), cancellationToken),
                export => Results.File(Encoding.UTF8.GetBytes(export.Content), export.ContentType, export.FileName)));

        group.MapPut("/sessions/{sessionId:guid}/answers/{questionId:guid}/score", (Guid sessionId, Guid questionId, ManualScoreRequest request, IAppMediator mediator, CancellationToken cancellationToken) =>
            EndpointHelpers.ExecuteAsync(() => mediator.SendAsync(new ManualScoreAnswerCommand(sessionId, questionId, request), cancellationToken)));

        group.MapGet("/exams/{examScheduleId:guid}/stream", StreamExamAsync);

        return group;
    }

    private static async Task StreamExamAsync(
        Guid examScheduleId,
        HttpContext httpContext,
        IDashboardEventBus eventBus,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        httpContext.Response.ContentType = "text/event-stream";

        await httpContext.Response.StartAsync(cancellationToken);
        await httpContext.Response.WriteAsync("retry: 3000\n: connected\n\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);

        await using var events = eventBus
            .SubscribeAsync(examScheduleId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        Task<bool>? pendingEvent = null;
        Task<bool>? pendingHeartbeat = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                pendingEvent ??= events.MoveNextAsync().AsTask();
                pendingHeartbeat ??= heartbeatTimer.WaitForNextTickAsync(cancellationToken).AsTask();
                var completed = await Task.WhenAny(pendingEvent, pendingHeartbeat);
                if (completed == pendingHeartbeat)
                {
                    if (!await pendingHeartbeat)
                    {
                        break;
                    }

                    pendingHeartbeat = null;
                    await httpContext.Response.WriteAsync(
                        $": heartbeat {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n\n",
                        cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                    continue;
                }

                if (!await pendingEvent)
                {
                    break;
                }

                var liveEvent = events.Current;
                pendingEvent = null;
                await httpContext.Response.WriteAsync($"event: {liveEvent.Type}\n", cancellationToken);
                await httpContext.Response.WriteAsync(
                    $"data: {JsonSerializer.Serialize(liveEvent, WebJsonOptions)}\n\n",
                    cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the client disconnects or refreshes - fall through to cleanup below.
        }
        finally
        {
            // The compiler-generated DisposeAsync for `events` (an async-iterator enumerator)
            // cannot run while a MoveNextAsync() call from it is still outstanding - doing so
            // throws NotSupportedException and skips SubscribeAsync's own cleanup, leaking its
            // Redis subscription. Cancellation here can unwind the loop above (via the heartbeat
            // task) before that pending MoveNextAsync() has settled, so it must be awaited here
            // - before the `await using events` above runs - no matter how the loop exited.
            if (pendingEvent is not null)
            {
                try
                {
                    await pendingEvent;
                }
                catch
                {
                    // The subscription is being torn down regardless of how this task completes.
                }
            }
        }
    }
}
