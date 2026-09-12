namespace BehavioralAnticheatEngine.Application.Behavior;

public interface IBehavioralEventProcessorRegistry
{
    IBehavioralEventProcessor Resolve(string eventType);
}
