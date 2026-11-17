using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Detection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BehavioralAnticheatEngine.Infrastructure.Workers;

/// <summary>
/// Safety net of the detection queue, every SweepIntervalSeconds:
///  - re-queues sessions stuck in "processing" (lost message, Worker crash);
///  - retries failed detections (ml-service down) up to MaxAttempts, then
///    leaves them failed;
///  - queues submitted sessions that have no detection at all (submitted
///    before the AI scoring existed, or the API could not record the request).
/// The database row is the source of truth; the queue only triggers work.
/// </summary>
public sealed class DetectionRecoveryHostedService : BackgroundService
{
    private const int SweepLimit = 200;

    private readonly IServiceProvider _serviceProvider;
    private readonly DetectionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DetectionRecoveryHostedService> _logger;

    public DetectionRecoveryHostedService(
        IServiceProvider serviceProvider,
        IOptions<DetectionOptions> options,
        TimeProvider timeProvider,
        ILogger<DetectionRecoveryHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the consumer join its group first.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.SweepIntervalSeconds));
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Detection recovery sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var detections = scope.ServiceProvider.GetRequiredService<IDetectionRepository>();
        var requests = scope.ServiceProvider.GetRequiredService<IDetectionRequestService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = _timeProvider.GetUtcNow();

        var stuck = await detections.ListNeedingRecoveryAsync(
            now.AddMinutes(-_options.StaleProcessingMinutes),
            now.AddMinutes(-_options.RetryFailedAfterMinutes),
            _options.MaxAttempts,
            SweepLimit,
            cancellationToken);

        var requeued = 0;
        foreach (var detection in stuck)
        {
            if (detection.Attempts >= _options.MaxAttempts)
            {
                // Out of attempts while "processing" (e.g. crashed every time): give up visibly.
                detection.Fail($"Gave up after {detection.Attempts} attempts.", now);
                continue;
            }

            await requests.RequestAsync(detection.SessionId, detection.ExamScheduleId, detection.RequestedAtUtc, cancellationToken);
            requeued++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var backfilled = 0;
        if (_options.BackfillSubmittedSessions)
        {
            foreach (var (sessionId, examScheduleId, submittedAtUtc) in await detections.ListSubmittedWithoutDetectionAsync(_options.BackfillBatchSize, cancellationToken))
            {
                await requests.RequestAsync(sessionId, examScheduleId, submittedAtUtc, cancellationToken);
                backfilled++;
            }
        }

        if (requeued > 0 || backfilled > 0)
        {
            _logger.LogInformation("Detection recovery: re-queued {Requeued}, backfilled {Backfilled} submitted sessions.", requeued, backfilled);
        }
    }
}
