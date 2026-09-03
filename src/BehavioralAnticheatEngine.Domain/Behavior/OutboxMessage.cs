namespace BehavioralAnticheatEngine.Domain.Behavior;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(string type, string payloadJson, DateTimeOffset occurredAtUtc)
    {
        Id = Guid.NewGuid();
        Type = type;
        PayloadJson = payloadJson;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage Create(string type, string payloadJson, DateTimeOffset occurredAtUtc)
    {
        return new OutboxMessage(type, payloadJson, occurredAtUtc);
    }

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
    }
}
