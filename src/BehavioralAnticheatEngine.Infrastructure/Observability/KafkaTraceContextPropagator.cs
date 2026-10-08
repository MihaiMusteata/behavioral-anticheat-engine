using System.Diagnostics;
using System.Text;
using Confluent.Kafka;

namespace BehavioralAnticheatEngine.Infrastructure.Observability;

public static class KafkaTraceContextPropagator
{
    private const string TraceParentHeader = "traceparent";

    public static void Inject(Activity? activity, Headers headers)
    {
        if (activity is null)
        {
            return;
        }

        headers.Add(TraceParentHeader, Encoding.UTF8.GetBytes(activity.Id ?? string.Empty));
    }

    public static ActivityContext Extract(Headers headers)
    {
        if (headers.TryGetLastBytes(TraceParentHeader, out var bytes))
        {
            var traceParent = Encoding.UTF8.GetString(bytes);
            if (ActivityContext.TryParse(traceParent, traceState: null, out var context))
            {
                return context;
            }
        }

        return default;
    }
}
