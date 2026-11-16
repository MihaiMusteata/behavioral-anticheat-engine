using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Exams;

namespace BehavioralAnticheatEngine.Application.Detection;

/// <summary>
/// Builds the ml-service payload of one session from the database - the same
/// raw tables the models were trained on (exam_sessions, questions,
/// student_answers, behavioral_events, calibration_samples). Contract:
/// ml/inference/features.py. All cleaning happens in Python, identically to
/// training, so nothing is filtered here.
/// </summary>
public sealed class DetectionPayloadBuilder
{
    private readonly IExamRepository _exams;
    private readonly IBehavioralEventRepository _events;

    public DetectionPayloadBuilder(IExamRepository exams, IBehavioralEventRepository events)
    {
        _exams = exams;
        _events = events;
    }

    public async Task<(JsonObject Payload, ExamSession Session)?> BuildAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _exams.GetSessionWithAssessmentAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var events = await _events.ListEventsForSessionAsync(sessionId, cancellationToken);
        var answers = await _exams.ListAnswersForSessionAsync(sessionId, cancellationToken);
        var calibration = await _exams.ListCalibrationSamplesForSessionAsync(sessionId, cancellationToken);
        var questions = session.Assessment?.Questions.OrderBy(question => question.OrderIndex).ToArray() ?? Array.Empty<Question>();

        var payload = new JsonObject
        {
            ["sessionId"] = session.Id.ToString(),
            ["status"] = session.Status,
            ["startedAtUtc"] = Iso(session.StartedAtUtc),
            ["submittedAtUtc"] = session.SubmittedAtUtc is null ? null : Iso(session.SubmittedAtUtc.Value),
            ["lastActivityAtUtc"] = Iso(session.LastActivityAtUtc),
            ["questions"] = new JsonArray(questions
                .Select(question => (JsonNode)new JsonObject
                {
                    ["questionId"] = question.Id.ToString(),
                    ["type"] = question.Type
                })
                .ToArray()),
            ["answers"] = new JsonArray(answers
                .Select(answer => (JsonNode)new JsonObject { ["questionId"] = answer.QuestionId.ToString() })
                .ToArray()),
            ["events"] = new JsonArray(events
                .OrderBy(behavioralEvent => behavioralEvent.Seq)
                .Select(behavioralEvent => (JsonNode)new JsonObject
                {
                    ["id"] = behavioralEvent.Id.ToString(),
                    ["seq"] = behavioralEvent.Seq,
                    ["eventType"] = behavioralEvent.EventType,
                    ["timestampUtc"] = Iso(behavioralEvent.TimestampUtc),
                    ["receivedAtUtc"] = Iso(behavioralEvent.ReceivedAtUtc),
                    ["questionId"] = behavioralEvent.QuestionId?.ToString(),
                    ["payload"] = ParseObject(behavioralEvent.PayloadJson),
                    ["validationFlags"] = behavioralEvent.ValidationFlags
                })
                .ToArray()),
            ["calibration"] = new JsonArray(calibration
                .Select(sample => (JsonNode)new JsonObject
                {
                    ["fieldName"] = sample.FieldName,
                    ["characterCount"] = sample.CharacterCount,
                    ["totalDurationMs"] = sample.TotalDurationMs,
                    ["averageCharsPerSecond"] = sample.AverageCharsPerSecond,
                    ["correctionCount"] = sample.CorrectionCount,
                    ["keystrokeTimings"] = ParseArray(sample.KeystrokeTimingsJson)
                })
                .ToArray())
        };

        return (payload, session);
    }

    private static string Iso(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static JsonArray ParseArray(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonArray ?? new JsonArray();
        }
        catch (JsonException)
        {
            return new JsonArray();
        }
    }
}
