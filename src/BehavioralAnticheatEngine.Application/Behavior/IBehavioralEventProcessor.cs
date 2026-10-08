using BehavioralAnticheatEngine.Domain.Behavior;

namespace BehavioralAnticheatEngine.Application.Behavior;

public interface IBehavioralEventProcessor
{
    string EventType { get; }

    Task ProcessAsync(BehavioralEventRecord eventRecord, SessionFeatureAggregate aggregate, CancellationToken cancellationToken = default);
}
