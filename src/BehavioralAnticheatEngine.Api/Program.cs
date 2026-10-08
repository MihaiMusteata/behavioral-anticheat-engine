using System;
using BehavioralAnticheatEngine.Api.Endpoints;
using BehavioralAnticheatEngine.Api.Extensions;
using BehavioralAnticheatEngine.Application;
using BehavioralAnticheatEngine.Infrastructure;
using BehavioralAnticheatEngine.Infrastructure.Observability;
using BehavioralAnticheatEngine.Infrastructure.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Defense-in-depth backstop for the whole API (not just one endpoint): cuts
// Kestrel's 30MB default roughly in half. Sized to comfortably clear the
// largest legitimate payload today - a full behavioral-event batch
// (MaxBatchSize x MaxEncryptedDataLength, ~9MB) - while still rejecting
// grossly oversized/abusive request bodies before they reach app code.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 16 * 1024 * 1024;
});

var redisConnection = RedisConnectionFactory.Create(builder.Configuration);

builder.Services.AddApiSwagger();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, redisConnection);
builder.Services.AddObservability(
    builder.Configuration,
    serviceName: "behavioral-anticheat-api",
    redisConnection: redisConnection,
    configureTracing: tracing => tracing.AddAspNetCoreInstrumentation(),
    configureMetrics: metrics => metrics.AddAspNetCoreInstrumentation());
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("client", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseApiSwagger();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("client");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapAssessmentEndpoints();
app.MapExamScheduleEndpoints();
app.MapStudentExamEndpoints();
app.MapGuestEndpoints();
app.MapBehavioralEventEndpoints();
app.MapDashboardEndpoints();

app.MapGet("/api/status", () =>
{
    return Results.Ok(new
    {
        service = "Behavioral Anticheat Engine API",
        status = "running",
        timestamp = DateTimeOffset.UtcNow
    });
})
.WithName("GetApiStatus");

app.MapGet("/api/secure-status", () =>
{
    return Results.Ok(new
    {
        service = "Behavioral Anticheat Engine API",
        status = "authorized",
        timestamp = DateTimeOffset.UtcNow
    });
})
.RequireAuthorization()
.WithName("GetSecureApiStatus");

app.Run();
