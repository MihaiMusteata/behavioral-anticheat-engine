using BehavioralAnticheatEngine.Application.Behavior.Contracts;

namespace BehavioralAnticheatEngine.Application.Behavior;

public interface IBehavioralIngestionService
{
    Task<BehavioralEventBatchResult> IngestAsync(Guid studentId, BehavioralEventBatchRequest request, CancellationToken cancellationToken = default);
}
