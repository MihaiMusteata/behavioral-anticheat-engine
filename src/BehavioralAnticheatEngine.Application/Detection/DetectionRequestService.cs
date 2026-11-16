using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Behavior;
using Microsoft.Extensions.Logging;

namespace BehavioralAnticheatEngine.Application.Detection;

public sealed class DetectionRequestService : IDetectionRequestService
{
    private readonly IDetectionRepository _detections;
    private readonly IDetectionQueue _queue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DetectionRequestService> _logger;

    public DetectionRequestService(
        IDetectionRepository detections,
        IDetectionQueue queue,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<DetectionRequestService> logger)
    {
        _detections = detections;
        _queue = queue;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RequestAsync(Guid sessionId, Guid examScheduleId, DateTimeOffset submittedAtUtc, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var detection = await _detections.GetAsync(sessionId, cancellationToken);
        if (detection is null)
        {
            await _detections.AddAsync(SessionDetection.Request(sessionId, examScheduleId, now), cancellationToken);
        }
        else
        {
            detection.Requeue(now);
        }

        // The row is the source of truth ("processing" from this moment on);
        // the queue message only triggers the work. If publishing fails, the
        // Worker's recovery sweep finds the stale row and re-queues it, so a
        // submit never fails because of the AI pipeline.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        try
        {
            await _queue.EnqueueAsync(new DetectionRequest(sessionId, examScheduleId, submittedAtUtc), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not enqueue detection for session {SessionId}; recovery sweep will retry.", sessionId);
        }
    }
}
