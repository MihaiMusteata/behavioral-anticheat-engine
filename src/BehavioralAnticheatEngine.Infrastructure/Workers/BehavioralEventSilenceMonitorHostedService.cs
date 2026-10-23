using System.Security.Cryptography;
using System.Text.Json;
using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;
using BehavioralAnticheatEngine.Domain.Exams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BehavioralAnticheatEngine.Infrastructure.Workers;

public sealed class BehavioralEventSilenceMonitorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly BehavioralIngestionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BehavioralEventSilenceMonitorHostedService> _logger;
    private readonly Dictionary<Guid, DateTimeOffset> _lastPublishedBySession = new();

    public BehavioralEventSilenceMonitorHostedService(
        IServiceProvider serviceProvider,
        IOptions<BehavioralIngestionOptions> options,
        TimeProvider timeProvider,
        ILogger<BehavioralEventSilenceMonitorHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(_options.EventSilenceSeconds / 2, 15)));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PublishSilenceEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Behavioral silence monitor failed; it will retry on the next interval.");
            }
        }
    }

    private async Task PublishSilenceEventsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IBehavioralEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var liveState = scope.ServiceProvider.GetRequiredService<ILiveExamStateStore>();
        var dashboardEvents = scope.ServiceProvider.GetRequiredService<IDashboardEventBus>();
        var now = _timeProvider.GetUtcNow();
        var silenceThreshold = TimeSpan.FromSeconds(_options.EventSilenceSeconds);
        var abandonmentThreshold = TimeSpan.FromSeconds(_options.SessionAbandonmentSeconds);
        var sessions = await exams.ListActiveSessionsAsync(now, cancellationToken);

        foreach (var session in sessions)
        {
            var silentFor = now - session.LastActivityAtUtc;
            if (silentFor < silenceThreshold)
            {
                continue;
            }

            if (silentFor >= abandonmentThreshold)
            {
                await AbandonSessionAsync(session.Id, now, exams, unitOfWork, publisher, liveState, dashboardEvents, cancellationToken);
                _lastPublishedBySession.Remove(session.Id);
                continue;
            }

            if (_lastPublishedBySession.TryGetValue(session.Id, out var lastPublished) &&
                now - lastPublished < silenceThreshold)
            {
                continue;
            }

            var behavioralEvent = new ValidatedBehavioralEvent(
                session.Id,
                session.ExamScheduleId,
                session.StudentId,
                -RandomNumberGenerator.GetInt32(1, int.MaxValue),
                now,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)),
                null,
                "event_silence_detected",
                JsonSerializer.Serialize(new
                {
                    silentForMs = (long)silentFor.TotalMilliseconds,
                    lastActivityAtUtc = session.LastActivityAtUtc
                }),
                "server",
                "server_generated",
                now);

            await publisher.PublishAsync(behavioralEvent, cancellationToken);
            _lastPublishedBySession[session.Id] = now;
        }
    }

    // Fetches a freshly-tracked copy rather than mutating the AsNoTracking()
    // instance from ListActiveSessionsAsync - that one can't be saved.
    private static async Task AbandonSessionAsync(
        Guid sessionId,
        DateTimeOffset now,
        IExamRepository exams,
        IUnitOfWork unitOfWork,
        IBehavioralEventPublisher publisher,
        ILiveExamStateStore liveState,
        IDashboardEventBus dashboardEvents,
        CancellationToken cancellationToken)
    {
        var trackedSession = await exams.GetSessionAsync(sessionId, cancellationToken);
        if (trackedSession is null || trackedSession.Status != ExamSessionStatuses.Active)
        {
            // Already transitioned (submitted, or abandoned by a previous tick) - nothing to do.
            return;
        }

        trackedSession.MarkAbandoned();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        const string eventType = "session_abandoned";
        var behavioralEvent = new ValidatedBehavioralEvent(
            trackedSession.Id,
            trackedSession.ExamScheduleId,
            trackedSession.StudentId,
            -RandomNumberGenerator.GetInt32(1, int.MaxValue),
            now,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)),
            null,
            eventType,
            JsonSerializer.Serialize(new
            {
                lastActivityAtUtc = trackedSession.LastActivityAtUtc
            }),
            "server",
            "server_generated",
            now);

        await publisher.PublishAsync(behavioralEvent, cancellationToken);

        var existingState = (await liveState.ListByExamAsync(trackedSession.ExamScheduleId, cancellationToken))
            .FirstOrDefault(state => state.SessionId == trackedSession.Id);
        var updatedState = new LiveSessionState(
            trackedSession.ExamScheduleId,
            trackedSession.Id,
            trackedSession.StudentId,
            trackedSession.ParticipantName,
            trackedSession.Status,
            trackedSession.CurrentRiskScore,
            existingState?.TotalEvents ?? 0,
            trackedSession.LastActivityAtUtc,
            eventType);

        await liveState.UpsertAsync(updatedState, cancellationToken);
        await dashboardEvents.PublishAsync(
            new DashboardLiveEvent(
                trackedSession.ExamScheduleId,
                trackedSession.Id,
                trackedSession.StudentId,
                eventType,
                JsonSerializer.Serialize(updatedState),
                now),
            cancellationToken);
    }
}
