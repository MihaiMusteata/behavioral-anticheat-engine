using BehavioralAnticheatEngine.Application.Common.Interfaces;
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

        await SeedConfiguredUsersAsync(scope.ServiceProvider, dbContext, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
