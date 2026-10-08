using System.Text.Json;
using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;
using BehavioralAnticheatEngine.Domain.Behavior;

namespace BehavioralAnticheatEngine.Application.Behavior;

public sealed class BehavioralEventProcessingService : IBehavioralEventProcessingService
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IBehavioralEventRepository _events;
    private readonly IExamRepository _exams;
    private readonly IUserRepository _users;
    private readonly IBehavioralEventProcessorRegistry _processors;
    private readonly IRiskScoringService _riskScoring;
    private readonly ILiveExamStateStore _liveState;
    private readonly IDashboardEventBus _dashboardEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public BehavioralEventProcessingService(
        IBehavioralEventRepository events,
        IExamRepository exams,
        IUserRepository users,
        IBehavioralEventProcessorRegistry processors,
        IRiskScoringService riskScoring,
        ILiveExamStateStore liveState,
        IDashboardEventBus dashboardEvents,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _events = events;
        _exams = exams;
        _users = users;
        _processors = processors;
        _riskScoring = riskScoring;
        _liveState = liveState;
        _dashboardEvents = dashboardEvents;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task ProcessAsync(ValidatedBehavioralEvent behavioralEvent, CancellationToken cancellationToken = default)
    {
        if (await _events.EventExistsAsync(behavioralEvent.SessionId, behavioralEvent.Seq, cancellationToken))
        {
            return;
        }

        var eventRecord = BehavioralEventRecord.Create(
            behavioralEvent.SessionId,
            behavioralEvent.ExamScheduleId,
            behavioralEvent.StudentId,
            behavioralEvent.Seq,
            behavioralEvent.TimestampUtc,
            behavioralEvent.Nonce,
            behavioralEvent.QuestionId,
            behavioralEvent.EventType,
            behavioralEvent.PayloadJson,
            behavioralEvent.Signature,
            behavioralEvent.ValidationFlags,
            behavioralEvent.ReceivedAtUtc);

        await _events.AddEventAsync(eventRecord, cancellationToken);

        var aggregate = await _events.GetFeatureAggregateAsync(behavioralEvent.SessionId, cancellationToken);
        if (aggregate is null)
        {
            aggregate = SessionFeatureAggregate.Create(
                behavioralEvent.SessionId,
                behavioralEvent.ExamScheduleId,
                behavioralEvent.StudentId,
                behavioralEvent.ReceivedAtUtc);
            await _events.AddFeatureAggregateAsync(aggregate, cancellationToken);
        }

        var processor = _processors.Resolve(behavioralEvent.EventType);
        await processor.ProcessAsync(eventRecord, aggregate, cancellationToken);

        var score = await _riskScoring.ScoreAsync(aggregate, cancellationToken);
        aggregate.SetRiskScore(score.Score, _timeProvider.GetUtcNow());
        await _events.AddRiskSnapshotAsync(
            RiskScoreSnapshot.Create(
                behavioralEvent.SessionId,
                behavioralEvent.ExamScheduleId,
                behavioralEvent.StudentId,
                score.Score,
                score.Reason,
                _timeProvider.GetUtcNow()),
            cancellationToken);

        var session = await _exams.GetSessionAsync(behavioralEvent.SessionId, cancellationToken);
        if (session is not null)
        {
            var serverGenerated = HasValidationFlag(behavioralEvent.ValidationFlags, "server_generated");
            if (!serverGenerated)
            {
                session.MarkActivity(behavioralEvent.ReceivedAtUtc);
            }

            session.UpdateRisk(score.Score);
            if (HasValidationFlag(behavioralEvent.ValidationFlags, "seq_gap"))
            {
                session.RecordSequenceGap();
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var student = await _users.GetByIdAsync(behavioralEvent.StudentId, cancellationToken);
        var liveState = new LiveSessionState(
            behavioralEvent.ExamScheduleId,
            behavioralEvent.SessionId,
            behavioralEvent.StudentId,
            session?.ParticipantName ?? student?.Email ?? string.Empty,
            session?.Status ?? string.Empty,
            score.Score,
            aggregate.TotalEvents,
            session?.LastActivityAtUtc ?? behavioralEvent.TimestampUtc,
            behavioralEvent.EventType);

        await _liveState.UpsertAsync(liveState, cancellationToken);
        await _dashboardEvents.PublishAsync(
            new DashboardLiveEvent(
                behavioralEvent.ExamScheduleId,
                behavioralEvent.SessionId,
                behavioralEvent.StudentId,
                "behavior.event",
                JsonSerializer.Serialize(new
                {
                    liveState,
                    eventRecord.Id,
                    eventRecord.Seq,
                    eventRecord.EventType,
                    eventRecord.QuestionId,
                    eventRecord.PayloadJson,
                    eventRecord.TimestampUtc,
                    eventRecord.ValidationFlags,
                    eventRecord.ReceivedAtUtc
                }, WebJsonOptions),
                _timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static bool HasValidationFlag(string validationFlags, string expected)
    {
        return validationFlags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(expected, StringComparer.OrdinalIgnoreCase);
    }
}
