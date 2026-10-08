using BehavioralAnticheatEngine.Application.Behavior.Contracts;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IBehavioralEventPublisher
{
    Task PublishAsync(ValidatedBehavioralEvent behavioralEvent, CancellationToken cancellationToken = default);
}
