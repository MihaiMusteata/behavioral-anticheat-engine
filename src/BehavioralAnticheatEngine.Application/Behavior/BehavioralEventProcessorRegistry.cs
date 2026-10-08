namespace BehavioralAnticheatEngine.Application.Behavior;

public sealed class BehavioralEventProcessorRegistry : IBehavioralEventProcessorRegistry
{
    private readonly IReadOnlyDictionary<string, IBehavioralEventProcessor> _processors;
    private readonly IBehavioralEventProcessor _fallback;

    public BehavioralEventProcessorRegistry(IEnumerable<IBehavioralEventProcessor> processors)
    {
        var materialized = processors.ToArray();
        _fallback = materialized.Single(processor => processor.EventType == GenericBehavioralEventProcessor.FallbackEventType);
        _processors = materialized
            .Where(processor => processor.EventType != GenericBehavioralEventProcessor.FallbackEventType)
            .ToDictionary(processor => processor.EventType, StringComparer.OrdinalIgnoreCase);
    }

    public IBehavioralEventProcessor Resolve(string eventType)
    {
        return _processors.TryGetValue(eventType, out var processor) ? processor : _fallback;
    }
}
