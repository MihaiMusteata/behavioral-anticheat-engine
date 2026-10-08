using BehavioralAnticheatEngine.Application.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        IConnectionMultiplexer redisConnection,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var environment = configuration["OpenTelemetry:Environment"] ?? "production";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceInstanceId: Environment.MachineName)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environment)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(KafkaActivitySource.Name)
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddRedisInstrumentation(redisConnection)
                    .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));

                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(AppMetrics.MeterName)
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));

                configureMetrics?.Invoke(metrics);
            });

        services.AddLogging(logging => logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
            options.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
        }));

        return services;
    }
}
