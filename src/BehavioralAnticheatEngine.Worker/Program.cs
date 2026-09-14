using BehavioralAnticheatEngine.Application;
using BehavioralAnticheatEngine.Infrastructure;
using BehavioralAnticheatEngine.Infrastructure.Observability;
using BehavioralAnticheatEngine.Infrastructure.Redis;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var redisConnection = RedisConnectionFactory.Create(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, redisConnection, initializeDatabase: false);
builder.Services.AddObservability(
    builder.Configuration,
    serviceName: "behavioral-anticheat-worker",
    redisConnection: redisConnection);
builder.Services.AddAnticheatWorkerServices();

await builder.Build().RunAsync();
