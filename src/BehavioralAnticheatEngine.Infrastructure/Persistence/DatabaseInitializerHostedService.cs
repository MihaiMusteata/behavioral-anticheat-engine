using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Behavior;
using BehavioralAnticheatEngine.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BehavioralAnticheatEngine.Infrastructure.Persistence;

public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;

    public DatabaseInitializerHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DatabaseInitializerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _logger.LogInformation("Applying application database migrations.");
        await dbContext.Database.MigrateAsync(cancellationToken);

        await SeedScoringRulesAsync(dbContext, _configuration, cancellationToken);
        await SeedConfiguredUsersAsync(scope.ServiceProvider, dbContext, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static async Task SeedScoringRulesAsync(
        AppDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var rules = configuration
            .GetSection("RiskScoring:Rules")
            .Get<Dictionary<string, double>>()
            ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (rules.Count == 0)
        {
            return;
        }

        // Configuration is the source of truth for the placeholder scorer. Rules
        // removed from configuration must stop contributing after the next start.
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE scoring_rules SET \"IsEnabled\" = FALSE;",
            cancellationToken);

        foreach (var rule in rules)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO scoring_rules ("Id", "FeatureKey", "Weight", "IsEnabled")
                VALUES ({Guid.NewGuid()}, {rule.Key}, {rule.Value}, TRUE)
                ON CONFLICT ("FeatureKey") DO UPDATE
                SET "Weight" = EXCLUDED."Weight", "IsEnabled" = TRUE;
                """,
                cancellationToken);
        }
    }

    private async Task SeedConfiguredUsersAsync(
        IServiceProvider serviceProvider,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seedUsers = _configuration.GetSection("SeedUsers").Get<SeedUserOptions[]>() ?? [];
        if (seedUsers.Length == 0)
        {
            return;
        }

        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();

        foreach (var seedUser in seedUsers)
        {
            if (string.IsNullOrWhiteSpace(seedUser.Email) ||
                string.IsNullOrWhiteSpace(seedUser.Password) ||
                !UserRoles.All.Contains(seedUser.Role, StringComparer.Ordinal))
            {
                continue;
            }

            var normalizedEmail = seedUser.Email.Trim().ToUpperInvariant();
            var exists = await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
            if (exists)
            {
                continue;
            }

            await dbContext.Users.AddAsync(
                User.Create(
                    seedUser.Email.Trim(),
                    normalizedEmail,
                    passwordHasher.Hash(seedUser.Password),
                    TimeProvider.System.GetUtcNow(),
                    seedUser.Role),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedUserOptions
    {
        public string Email { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;

        public string Role { get; init; } = UserRoles.Student;
    }
}
