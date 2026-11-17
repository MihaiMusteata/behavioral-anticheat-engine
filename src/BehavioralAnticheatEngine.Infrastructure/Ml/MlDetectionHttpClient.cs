using System.Net.Http.Json;
using System.Text.Json.Nodes;
using BehavioralAnticheatEngine.Application.Detection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BehavioralAnticheatEngine.Infrastructure.Ml;

public sealed record MlServiceOptions
{
    public const string SectionName = "MlService";

    public string BaseUrl { get; init; } = "http://localhost:8000";

    // A batch of sessions through all voters takes seconds to a few minutes
    // on CPU (TabPFN dominates) - generous on purpose.
    public int TimeoutSeconds { get; init; } = 600;
}

/// <summary>Calls ml-service (ml/service/app.py) - POST /v1/detect.</summary>
public sealed class MlDetectionHttpClient : IMlDetectionClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<MlDetectionHttpClient> _logger;

    public MlDetectionHttpClient(IOptions<MlServiceOptions> options, ILogger<MlDetectionHttpClient> logger)
    {
        _logger = logger;
        _http = new HttpClient
        {
            BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds)
        };
    }

    public async Task<IReadOnlyList<JsonObject>> DetectAsync(IReadOnlyList<JsonObject> payloads, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["sessions"] = new JsonArray(payloads.Select(payload => (JsonNode)payload.DeepClone()).ToArray()) };
        using var response = await _http.PostAsJsonAsync("v1/detect", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"ml-service returned {(int)response.StatusCode}: {Truncate(detail, 300)}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new HttpRequestException("ml-service returned an empty body.");
        var results = json["results"] as JsonArray ?? throw new HttpRequestException("ml-service response has no 'results'.");
        _logger.LogInformation("ml-service scored {Count} sessions in {Elapsed}s.", results.Count, json["elapsedSeconds"]?.ToString());
        return results.OfType<JsonObject>().ToArray();
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private static string Truncate(string value, int length)
    {
        return value.Length <= length ? value : value[..length];
    }
}
