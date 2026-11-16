using System.Text.Json;
using System.Text.Json.Nodes;
using BehavioralAnticheatEngine.Domain.Behavior;
using BehavioralAnticheatEngine.Domain.Exams;

namespace BehavioralAnticheatEngine.Application.Detection;

/// <summary>SessionDetection row (+ session status) -> what the dashboard shows.</summary>
public static class DetectionMapper
{
    public static SessionDetectionSummaryDto ToSummary(SessionDetection? detection, string sessionStatus)
    {
        if (detection is null)
        {
            // No row yet: still taking the exam (or abandoned) -> waiting.
            // A submitted session without a row is being backfilled by the
            // Worker's recovery sweep, so it is shown as processing.
            var status = sessionStatus == ExamSessionStatuses.Submitted
                ? SessionDetectionStatuses.Processing
                : DetectionDisplayStatuses.Waiting;
            return new SessionDetectionSummaryDto(status, null, null, null, null, null, [], null, null, null);
        }

        return new SessionDetectionSummaryDto(
            detection.Status,
            detection.Score,
            detection.Decision,
            detection.Level,
            detection.Method,
            detection.VotersUsed,
            ParseVotes(detection.ModelsJson),
            detection.RequestedAtUtc,
            detection.CompletedAtUtc,
            detection.Status == SessionDetectionStatuses.Failed ? detection.Error : null);
    }

    public static IReadOnlyDictionary<Guid, QuestionDetectionDto> ParseQuestions(SessionDetection? detection)
    {
        var result = new Dictionary<Guid, QuestionDetectionDto>();
        if (detection is null || detection.Status != SessionDetectionStatuses.Completed)
        {
            return result;
        }

        if (JsonNode.Parse(detection.QuestionsJson) is not JsonArray questions)
        {
            return result;
        }

        foreach (var node in questions.OfType<JsonObject>())
        {
            if (!Guid.TryParse(node["questionId"]?.GetValue<string>(), out var questionId))
            {
                continue;
            }

            var ensemble = node["ensemble"] as JsonObject;
            var evidence = new Dictionary<string, double?>();
            if (node["evidence"] is JsonObject evidenceNode)
            {
                foreach (var (key, value) in evidenceNode)
                {
                    evidence[key] = ReadDouble(value);
                }
            }

            result[questionId] = new QuestionDetectionDto(
                questionId,
                node["questionIndex"]?.GetValue<int>() ?? 0,
                ReadDouble(ensemble?["score"]),
                ensemble?["decision"]?.GetValue<string>(),
                ensemble?["level"]?.GetValue<string>(),
                ReadInt(ensemble?["suspectVotes"]),
                ReadInt(ensemble?["votersUsed"]),
                ParseVotes(node["models"]?.ToJsonString() ?? "{}"),
                evidence);
        }

        return result;
    }

    /// <summary>{"01_catboost": {"probability": 0.93, "vote": "suspect"}, ...} -> ordered list.</summary>
    public static IReadOnlyCollection<DetectionModelVoteDto> ParseVotes(string modelsJson)
    {
        if (JsonNode.Parse(modelsJson) is not JsonObject models)
        {
            return [];
        }

        // jsonb does not keep key order: sort by voter id (01_..05_).
        return models
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new DetectionModelVoteDto(
                pair.Key,
                ReadDouble((pair.Value as JsonObject)?["probability"]),
                (pair.Value as JsonObject)?["vote"]?.GetValue<string>() ?? "abstain"))
            .ToArray();
    }

    private static int? ReadInt(JsonNode? node)
    {
        return ReadDouble(node) is { } value ? (int)value : null;
    }

    private static double? ReadDouble(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        return value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : null;
    }
}
