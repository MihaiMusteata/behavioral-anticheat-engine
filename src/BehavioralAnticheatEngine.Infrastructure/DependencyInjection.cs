using BehavioralAnticheatEngine.Application.Auth;
using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Common.Security;
using BehavioralAnticheatEngine.Application.Detection;
using BehavioralAnticheatEngine.Infrastructure.Messaging;
using BehavioralAnticheatEngine.Infrastructure.Ml;
using BehavioralAnticheatEngine.Infrastructure.Persistence;
using BehavioralAnticheatEngine.Infrastructure.Persistence.Repositories;
using BehavioralAnticheatEngine.Infrastructure.Redis;
using BehavioralAnticheatEngine.Infrastructure.Security;
using BehavioralAnticheatEngine.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace BehavioralAnticheatEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IConnectionMultiplexer redisConnection,
        bool initializeDatabase = true)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<BehavioralIngestionOptions>(configuration.GetSection(BehavioralIngestionOptions.SectionName));
        services.Configure<SecureActionOptions>(configuration.GetSection(SecureActionOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<MlServiceOptions>(configuration.GetSection(MlServiceOptions.SectionName));
        services.Configure<DetectionOptions>(configuration.GetSection(DetectionOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        if (initializeDatabase)
        {
            services.AddHostedService<DatabaseInitializerHostedService>();
        }
        services.AddSingleton(redisConnection);

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IBehavioralEventRepository, BehavioralEventRepository>();
        services.AddScoped<IDetectionRepository, DetectionRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExamSessionSecretStore, RedisExamSessionSecretStore>();
        services.AddScoped<INonceStore, RedisNonceStore>();
        services.AddScoped<ISequenceStore, RedisSequenceStore>();
        services.AddScoped<IRateLimitStore, RedisRateLimitStore>();
        services.AddScoped<ILiveExamStateStore, RedisLiveExamStateStore>();
        services.AddSingleton<IDashboardEventBus, RedisDashboardEventBus>();
        services.AddSingleton<IBehavioralEventPublisher, KafkaBehavioralEventPublisher>();
        services.AddSingleton<IDetectionQueue, KafkaDetectionQueue>();
        services.AddSingleton<IMlDetectionClient, MlDetectionHttpClient>();

        return services;
    }

    public static IServiceCollection AddAnticheatWorkerServices(this IServiceCollection services)
    {
        services.AddHostedService<BehavioralEventConsumerHostedService>();
        services.AddHostedService<BehavioralEventSilenceMonitorHostedService>();
        services.AddHostedService<DetectionConsumerHostedService>();
        services.AddHostedService<DetectionRecoveryHostedService>();

        return services;
    }
}
