using System.Security.Cryptography;
using System.Text.Json;
using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
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
        var now = _timeProvider.GetUtcNow();
        var silenceThreshold = TimeSpan.FromSeconds(_options.EventSilenceSeconds);
        var sessions = await exams.ListActiveSessionsAsync(now, cancellationToken);

        foreach (var session in sessions)
        {
            var silentFor = now - session.LastActivityAtUtc;
            if (silentFor < silenceThreshold)
            {
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
}
