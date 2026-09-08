namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface ISequenceStore
{
    Task<SequenceValidationResult> TryAdvanceAsync(Guid sessionId, long seq, long largeGapThreshold, CancellationToken cancellationToken = default);
}
