namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public sealed record SequenceValidationResult(
    bool Accepted,
    bool GapDetected,
    bool LargeGap,
    long PreviousSeq);
