using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Behavior;
using BehavioralAnticheatEngine.Domain.Exams;
using BehavioralAnticheatEngine.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace BehavioralAnticheatEngine.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Assessment> Assessments => Set<Assessment>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    public DbSet<ExamSchedule> ExamSchedules => Set<ExamSchedule>();

    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();

    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();

    public DbSet<BehavioralEventRecord> BehavioralEvents => Set<BehavioralEventRecord>();

    public DbSet<SessionFeatureAggregate> SessionFeatureAggregates => Set<SessionFeatureAggregate>();

    public DbSet<RiskScoreSnapshot> RiskScoreSnapshots => Set<RiskScoreSnapshot>();

    public DbSet<ScoringRule> ScoringRules => Set<ScoringRule>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
