using System.Text;
using BehavioralAnticheatEngine.Application.Common.Exceptions;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Behavior;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;
using BehavioralAnticheatEngine.Domain.Behavior;
using BehavioralAnticheatEngine.Domain.Exams;

namespace BehavioralAnticheatEngine.Application.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly IExamRepository _exams;
    private readonly IBehavioralEventRepository _events;
    private readonly ILiveExamStateStore _liveState;
    private readonly IRiskScoringService _riskScoring;
    private readonly TimeProvider _timeProvider;

    public DashboardService(
        IExamRepository exams,
        IBehavioralEventRepository events,
        ILiveExamStateStore liveState,
        IRiskScoringService riskScoring,
        TimeProvider timeProvider)
    {
        _exams = exams;
        _events = events;
        _liveState = liveState;
        _riskScoring = riskScoring;
        _timeProvider = timeProvider;
    }

    public async Task<ExamDashboardDto> GetExamDashboardAsync(Guid examScheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _exams.GetExamScheduleAsync(examScheduleId, includeAssessment: true, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam schedule was not found.");
        var sessions = await _exams.ListSessionsForExamAsync(examScheduleId, cancellationToken);
        var liveStates = (await _liveState.ListByExamAsync(examScheduleId, cancellationToken))
            .ToDictionary(state => state.SessionId);

        var rows = new List<DashboardSessionRowDto>(sessions.Count);
        var eventFrequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
        {
            var events = await _events.ListEventsForSessionAsync(session.Id, cancellationToken);
            liveStates.TryGetValue(session.Id, out var live);

            rows.Add(new DashboardSessionRowDto(
                session.Id,
                session.StudentId,
                session.ParticipantName,
                session.Status,
                session.StartedAtUtc,
                live?.LastActivityAtUtc ?? session.LastActivityAtUtc,
                live?.RiskScore ?? session.CurrentRiskScore,
                live?.TotalEvents ?? events.Count,
                ResolveLiveStatus(session, live)));

            foreach (var behavioralEvent in events)
            {
                eventFrequencies[behavioralEvent.EventType] = eventFrequencies.TryGetValue(behavioralEvent.EventType, out var count)
                    ? count + 1
                    : 1;
            }
        }

        var riskDistribution = new RiskDistributionDto(
            rows.Count(row => row.RiskScore < 35),
            rows.Count(row => row.RiskScore >= 35 && row.RiskScore < 70),
            rows.Count(row => row.RiskScore >= 70));

        return new ExamDashboardDto(
            schedule.Id,
            schedule.Name,
            rows.OrderByDescending(row => row.RiskScore).ThenBy(row => row.StudentEmail).ToArray(),
            riskDistribution,
            eventFrequencies
                .OrderByDescending(pair => pair.Value)
                .Select(pair => new EventFrequencyDto(pair.Key, pair.Value))
                .ToArray());
    }

    public async Task<SessionTimelineDto> GetSessionTimelineAsync(Guid sessionId, Guid? questionId, CancellationToken cancellationToken = default)
    {
        var session = await _exams.GetSessionWithAssessmentAsync(sessionId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam session was not found.");
        var allEvents = await _events.ListEventsForSessionAsync(sessionId, cancellationToken);
        var events = questionId is null
            ? allEvents
            : FilterEventsForQuestion(allEvents, questionId.Value).ToList();
        var snapshots = await _events.ListRiskSnapshotsForSessionAsync(sessionId, cancellationToken);
        var answers = await _exams.ListAnswersForSessionAsync(sessionId, cancellationToken);
        var questions = session.Assessment?.Questions.OrderBy(question => question.OrderIndex).ToArray() ?? Array.Empty<Question>();
        var questionDtos = new List<TimelineQuestionDto>(questions.Length);
        foreach (var question in questions)
        {
            var questionEvents = FilterEventsForQuestion(allEvents, question.Id);
            var questionScore = await _riskScoring.ScoreEventsAsync(questionEvents.Select(behavioralEvent => behavioralEvent.EventType), cancellationToken);
            questionDtos.Add(new TimelineQuestionDto(
                question.Id,
                question.OrderIndex,
                $"Question {question.OrderIndex}",
                question.Prompt,
                questionScore.Score));
        }

        double? selectedQuestionRiskScore = null;
        if (questionId is not null)
        {
            var selectedScore = await _riskScoring.ScoreEventsAsync(events.Select(behavioralEvent => behavioralEvent.EventType), cancellationToken);
            selectedQuestionRiskScore = selectedScore.Score;
        }

        return new SessionTimelineDto(
            session.Id,
            session.StudentId,
            session.ParticipantName,
            session.Status,
            session.CurrentRiskScore,
            session.SelfReportedCheated,
            questionId,
            selectedQuestionRiskScore,
            questionDtos,
            answers.Select(answer =>
            {
                var question = session.Assessment?.Questions.FirstOrDefault(candidate => candidate.Id == answer.QuestionId);
                return new AnswerReviewDto(
                    answer.QuestionId,
                    question?.Prompt ?? "Unknown question",
                    answer.AnswerJson,
                    answer.RequiresManualScoring,
                    answer.AutoScore,
                    answer.ManualScore,
                    answer.ManualFeedback);
            }).ToArray(),
            events
                .OrderBy(behavioralEvent => behavioralEvent.ReceivedAtUtc)
                .ThenBy(behavioralEvent => behavioralEvent.TimestampUtc)
                .ThenBy(behavioralEvent => behavioralEvent.Seq)
                .Select(behavioralEvent => new TimelineEventDto(
                    behavioralEvent.Id,
                    behavioralEvent.Seq,
                    behavioralEvent.TimestampUtc,
                    behavioralEvent.ReceivedAtUtc,
                    behavioralEvent.QuestionId,
                    behavioralEvent.EventType,
                    behavioralEvent.PayloadJson,
                    behavioralEvent.ValidationFlags))
                .ToArray(),
            snapshots
                .OrderBy(snapshot => snapshot.CreatedAtUtc)
                .Select(snapshot => new RiskSnapshotDto(snapshot.Score, snapshot.Reason, snapshot.CreatedAtUtc))
                .ToArray());
    }

    public async Task<SessionTimelineExportDto> ExportSessionTimelineAsync(
        Guid sessionId,
        Guid? questionId,
        string? eventType,
        CancellationToken cancellationToken = default)
    {
        var timeline = await GetSessionTimelineAsync(sessionId, questionId, cancellationToken);
        var normalizedEventType = string.IsNullOrWhiteSpace(eventType) ? null : eventType.Trim();
        var events = timeline.Events
            .Where(behavioralEvent => normalizedEventType is null ||
                string.Equals(behavioralEvent.EventType, normalizedEventType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(behavioralEvent => behavioralEvent.ReceivedAtUtc)
            .ThenBy(behavioralEvent => behavioralEvent.TimestampUtc)
            .ThenBy(behavioralEvent => behavioralEvent.Seq)
            .ToArray();

        var csv = new StringBuilder();
        AppendCsvRow(csv, "Seq", "ClientTimestampUtc", "ReceivedAtUtc", "QuestionId", "EventType", "ValidationFlags", "PayloadJson");

        foreach (var behavioralEvent in events)
        {
            AppendCsvRow(
                csv,
                behavioralEvent.Seq.ToString(System.Globalization.CultureInfo.InvariantCulture),
                behavioralEvent.TimestampUtc.ToString("O"),
                behavioralEvent.ReceivedAtUtc.ToString("O"),
                behavioralEvent.QuestionId?.ToString("D") ?? string.Empty,
                behavioralEvent.EventType,
                behavioralEvent.ValidationFlags,
                behavioralEvent.PayloadJson);
        }

        var fileName = $"session-{timeline.SessionId:N}-{SanitizeFileNamePart(timeline.StudentEmail)}-events.csv";

        return new SessionTimelineExportDto(fileName, "text/csv; charset=utf-8", csv.ToString());
    }

    private string ResolveLiveStatus(ExamSession session, LiveSessionState? live)
    {
        if (session.Status != ExamSessionStatuses.Active)
        {
            return session.Status;
        }

        var lastEventType = live?.LastEventType ?? string.Empty;
        if (lastEventType is "window.blur" or "window_blur" or "visibility.hidden" or "visibility_change")
        {
            return "out_of_window";
        }

        var lastActivity = live?.LastActivityAtUtc ?? session.LastActivityAtUtc;
        return lastActivity < _timeProvider.GetUtcNow().AddSeconds(-60) ? "inactive" : "active";
    }

    private static IEnumerable<BehavioralEventRecord> FilterEventsForQuestion(
        IReadOnlyCollection<BehavioralEventRecord> events,
        Guid questionId)
    {
        var intervals = BuildQuestionIntervals(events, questionId);
        foreach (var behavioralEvent in events.OrderBy(behavioralEvent => behavioralEvent.ReceivedAtUtc).ThenBy(behavioralEvent => behavioralEvent.Seq))
        {
            if (behavioralEvent.QuestionId == questionId)
            {
                yield return behavioralEvent;
                continue;
            }

            if (behavioralEvent.QuestionId is null &&
                intervals.Any(interval => behavioralEvent.ReceivedAtUtc >= interval.Start && behavioralEvent.ReceivedAtUtc <= interval.End))
            {
                yield return behavioralEvent;
            }
        }
    }

    private static IReadOnlyCollection<(DateTimeOffset Start, DateTimeOffset End)> BuildQuestionIntervals(
        IReadOnlyCollection<BehavioralEventRecord> events,
        Guid questionId)
    {
        var intervals = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        DateTimeOffset? currentStart = null;

        foreach (var behavioralEvent in events
            .Where(behavioralEvent => behavioralEvent.QuestionId == questionId)
            .OrderBy(behavioralEvent => behavioralEvent.ReceivedAtUtc)
            .ThenBy(behavioralEvent => behavioralEvent.Seq))
        {
            if (string.Equals(behavioralEvent.EventType, "question_view_start", StringComparison.OrdinalIgnoreCase))
            {
                currentStart = behavioralEvent.ReceivedAtUtc;
            }
            else if (string.Equals(behavioralEvent.EventType, "question_view_end", StringComparison.OrdinalIgnoreCase) &&
                currentStart is { } start)
            {
                intervals.Add((start, behavioralEvent.ReceivedAtUtc));
                currentStart = null;
            }
        }

        if (currentStart is { } openStart)
        {
            intervals.Add((openStart, DateTimeOffset.MaxValue));
        }

        return intervals;
    }

    private static void AppendCsvRow(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string SanitizeFileNamePart(string value)
    {
        var safe = new string(value
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-')
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(safe) ? "student" : safe;
    }
}
