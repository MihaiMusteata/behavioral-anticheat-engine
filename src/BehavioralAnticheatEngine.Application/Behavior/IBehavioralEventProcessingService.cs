using BehavioralAnticheatEngine.Application.Behavior.Contracts;

namespace BehavioralAnticheatEngine.Application.Behavior;

public interface IBehavioralEventProcessingService
{
    Task ProcessAsync(ValidatedBehavioralEvent behavioralEvent, CancellationToken cancellationToken = default);
}
