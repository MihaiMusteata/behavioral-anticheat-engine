namespace BehavioralAnticheatEngine.Domain.Behavior;

public sealed class BehavioralEventRecord
{
    private BehavioralEventRecord()
    {
    }

    private BehavioralEventRecord(
        Guid sessionId,
        Guid examScheduleId,
        Guid studentId,
        long seq,
        DateTimeOffset timestampUtc,
        string nonce,
        Guid? questionId,
        string eventType,
        string payloadJson,
        string signature,
        string validationFlags,
        DateTimeOffset receivedAtUtc)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        ExamScheduleId = examScheduleId;
        StudentId = studentId;
        Seq = seq;
        TimestampUtc = timestampUtc;
        Nonce = nonce;
        QuestionId = questionId;
        EventType = eventType;
        PayloadJson = payloadJson;
        Signature = signature;
        ValidationFlags = validationFlags;
        ReceivedAtUtc = receivedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid ExamScheduleId { get; private set; }

    public Guid StudentId { get; private set; }

    public long Seq { get; private set; }

    public DateTimeOffset TimestampUtc { get; private set; }

    public string Nonce { get; private set; } = string.Empty;

    public Guid? QuestionId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = "{}";

    public string Signature { get; private set; } = string.Empty;

    public string ValidationFlags { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public static BehavioralEventRecord Create(
        Guid sessionId,
        Guid examScheduleId,
        Guid studentId,
        long seq,
        DateTimeOffset timestampUtc,
        string nonce,
        Guid? questionId,
        string eventType,
        string payloadJson,
        string signature,
        string validationFlags,
        DateTimeOffset receivedAtUtc)
    {
        return new BehavioralEventRecord(
            sessionId,
            examScheduleId,
            studentId,
            seq,
            timestampUtc,
            nonce,
            questionId,
            eventType,
            payloadJson,
            signature,
            validationFlags,
            receivedAtUtc);
    }
}
