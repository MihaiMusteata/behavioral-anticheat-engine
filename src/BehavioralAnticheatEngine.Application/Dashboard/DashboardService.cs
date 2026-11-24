using System.Text;
using BehavioralAnticheatEngine.Application.Common.Exceptions;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;
using BehavioralAnticheatEngine.Application.Detection;
using BehavioralAnticheatEngine.Domain.Behavior;
using BehavioralAnticheatEngine.Domain.Exams;

namespace BehavioralAnticheatEngine.Application.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly IExamRepository _exams;
    private readonly IBehavioralEventRepository _events;
    private readonly ILiveExamStateStore _liveState;
    private readonly IDetectionRepository _detections;
    private readonly TimeProvider _timeProvider;

    public DashboardService(
        IExamRepository exams,
        IBehavioralEventRepository events,
        ILiveExamStateStore liveState,
        IDetectionRepository detections,
        TimeProvider timeProvider)
    {
        _exams = exams;
        _events = events;
        _liveState = liveState;
        _detections = detections;
        _timeProvider = timeProvider;
    }

    public async Task<ExamDashboardDto> GetExamDashboardAsync(Guid examScheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _exams.GetExamScheduleAsync(examScheduleId, includeAssessment: true, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam schedule was not found.");
        var sessions = await _exams.ListSessionsForExamAsync(examScheduleId, cancellationToken);
        var liveStates = (await _liveState.ListByExamAsync(examScheduleId, cancellationToken))
            .ToDictionary(state => state.SessionId);
        var detections = (await _detections.ListForSessionsAsync(sessions.Select(session => session.Id).ToArray(), cancellationToken))
            .ToDictionary(detection => detection.SessionId);

        var rows = new List<DashboardSessionRowDto>(sessions.Count);
        var eventFrequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
        {
            var events = await _events.ListEventsForSessionAsync(session.Id, cancellationToken);
            liveStates.TryGetValue(session.Id, out var live);
            detections.TryGetValue(session.Id, out var detection);

            rows.Add(new DashboardSessionRowDto(
                session.Id,
                session.StudentId,
                session.ParticipantName,
                session.Status,
                session.StartedAtUtc,
                live?.LastActivityAtUtc ?? session.LastActivityAtUtc,
                DetectionMapper.ToSummary(detection, session.Status),
                live?.TotalEvents ?? events.Count,
                ResolveLiveStatus(session, live)));

            foreach (var behavioralEvent in events)
            {
                eventFrequencies[behavioralEvent.EventType] = eventFrequencies.TryGetValue(behavioralEvent.EventType, out var count)
                    ? count + 1
                    : 1;
            }
        }

        var completed = rows.Where(row => row.Detection.Status == SessionDetectionStatuses.Completed).ToArray();
        var riskDistribution = new RiskDistributionDto(
            completed.Count(row => row.Detection.Level is not ("medium" or "high")),
            completed.Count(row => row.Detection.Level == "medium"),
            completed.Count(row => row.Detection.Level == "high"),
            rows.Count(row => row.Detection.Status is SessionDetectionStatuses.Processing or SessionDetectionStatuses.Failed),
            rows.Count(row => row.Detection.Status == DetectionDisplayStatuses.Waiting));

        return new ExamDashboardDto(
            schedule.Id,
            schedule.Name,
            rows.OrderByDescending(row => row.Detection.Score ?? -1)
                .ThenBy(row => row.StudentEmail)
                .ToArray(),
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
        var detection = await _detections.GetAsync(sessionId, cancellationToken);
        var questionDetections = DetectionMapper.ParseQuestions(detection);
        var answers = await _exams.ListAnswersForSessionAsync(sessionId, cancellationToken);
        var questions = session.Assessment?.Questions.OrderBy(question => question.OrderIndex).ToArray() ?? Array.Empty<Question>();
        var questionDtos = new List<TimelineQuestionDto>(questions.Length);
        foreach (var question in questions)
        {
            questionDetections.TryGetValue(question.Id, out var questionDetection);
            questionDtos.Add(new TimelineQuestionDto(
                question.Id,
                question.OrderIndex,
                $"Question {question.OrderIndex}",
                question.Prompt,
                questionDetection,
                question.Options
                    .OrderBy(option => option.OrderIndex)
                    .Select(option => new TimelineQuestionOptionDto(option.Id, option.Label))
                    .ToArray()));
        }

        return new SessionTimelineDto(
            session.Id,
            session.StudentId,
            session.ParticipantName,
            session.Status,
            DetectionMapper.ToSummary(detection, session.Status),
            session.SelfReportedCheated,
            questionId,
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
