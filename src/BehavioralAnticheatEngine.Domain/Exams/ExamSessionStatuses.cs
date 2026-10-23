namespace BehavioralAnticheatEngine.Domain.Exams;

public static class ExamSessionStatuses
{
    public const string Active = "active";
    public const string Submitted = "submitted";

    // A session the silence monitor gave up on (no activity for a long time,
    // e.g. the student closed the tab without submitting) - distinct from
    // Submitted so it's never scored, and distinct from staying "Active"
    // forever so it stops being polled and stops flooding the audit log
    // with repeated event_silence_detected entries. Still revivable: if the
    // student comes back with time left, MarkActivity() flips it back to
    // Active automatically.
    public const string Abandoned = "abandoned";
}
