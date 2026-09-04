using BehavioralAnticheatEngine.Domain.Behavior;
using BehavioralAnticheatEngine.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Configurations;

public sealed class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.ToTable("assessments");
        builder.HasKey(assessment => assessment.Id);
        builder.Property(assessment => assessment.Id).ValueGeneratedNever();
        builder.Property(assessment => assessment.Title).HasMaxLength(256).IsRequired();
        builder.Property(assessment => assessment.Description).HasMaxLength(2048);
        builder.Property(assessment => assessment.CreatedByUserId).IsRequired();
        builder.Property(assessment => assessment.CreatedAtUtc).IsRequired();
        builder.Property(assessment => assessment.IsPublished).IsRequired();
        builder.Property(assessment => assessment.IsDeleted).IsRequired();
        builder.HasIndex(assessment => assessment.IsDeleted);
        builder.HasMany(assessment => assessment.Questions)
            .WithOne(question => question.Assessment)
            .HasForeignKey(question => question.AssessmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(assessment => assessment.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Id).ValueGeneratedNever();
        builder.Property(question => question.Type).HasMaxLength(64).IsRequired();
        builder.Property(question => question.Prompt).HasMaxLength(4096).IsRequired();
        builder.Property(question => question.Points).HasPrecision(8, 2).IsRequired();
        builder.Property(question => question.OrderIndex).IsRequired();
        builder.HasIndex(question => new { question.AssessmentId, question.OrderIndex });
        builder.HasMany(question => question.Options)
            .WithOne(option => option.Question)
            .HasForeignKey(option => option.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(question => question.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ToTable("question_options");
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Id).ValueGeneratedNever();
        builder.Property(option => option.Label).HasMaxLength(2048).IsRequired();
        builder.Property(option => option.IsCorrect).IsRequired();
        builder.Property(option => option.OrderIndex).IsRequired();
        builder.HasIndex(option => new { option.QuestionId, option.OrderIndex });
    }
}

public sealed class ExamScheduleConfiguration : IEntityTypeConfiguration<ExamSchedule>
{
    public void Configure(EntityTypeBuilder<ExamSchedule> builder)
    {
        builder.ToTable("exam_schedules");
        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.Id).ValueGeneratedNever();
        builder.Property(schedule => schedule.Name).HasMaxLength(256).IsRequired();
        builder.Property(schedule => schedule.DurationMinutes).IsRequired();
        builder.Property(schedule => schedule.AccessPinCode).HasMaxLength(6);
        builder.Property(schedule => schedule.Status).HasMaxLength(64).IsRequired();
        builder.Property(schedule => schedule.CreatedByUserId).IsRequired();
        builder.Property(schedule => schedule.CreatedAtUtc).IsRequired();
        builder.HasOne(schedule => schedule.Assessment)
            .WithMany()
            .HasForeignKey(schedule => schedule.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(schedule => schedule.IsDeleted).IsRequired();
        builder.HasIndex(schedule => schedule.StartsAtUtc);
        builder.HasIndex(schedule => schedule.Status);
        builder.HasIndex(schedule => schedule.IsDeleted);
        builder.HasIndex(schedule => schedule.AccessPinCode).IsUnique();
    }
}

public sealed class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> builder)
    {
        builder.ToTable("exam_sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.ParticipantName).HasMaxLength(120).IsRequired();
        builder.Property(session => session.Status).HasMaxLength(64).IsRequired();
        builder.Property(session => session.StartedAtUtc).IsRequired();
        builder.Property(session => session.EndsAtUtc).IsRequired();
        builder.Property(session => session.LastActivityAtUtc).IsRequired();
        builder.Property(session => session.ConsentAcceptedAtUtc).IsRequired();
        builder.Property(session => session.CurrentRiskScore).IsRequired();
        builder.HasOne(session => session.ExamSchedule)
            .WithMany()
            .HasForeignKey(session => session.ExamScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(session => session.Assessment)
            .WithMany()
            .HasForeignKey(session => session.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(session => new { session.ExamScheduleId, session.StudentId, session.Status });
    }
}

public sealed class StudentAnswerConfiguration : IEntityTypeConfiguration<StudentAnswer>
{
    public void Configure(EntityTypeBuilder<StudentAnswer> builder)
    {
        builder.ToTable("student_answers");
        builder.HasKey(answer => answer.Id);
        builder.Property(answer => answer.Id).ValueGeneratedNever();
        builder.Property(answer => answer.AnswerJson).HasColumnType("jsonb").IsRequired();
        builder.Property(answer => answer.AnsweredAtUtc).IsRequired();
        builder.Property(answer => answer.AutoScore).HasPrecision(8, 2);
        builder.Property(answer => answer.ManualScore).HasPrecision(8, 2);
        builder.Property(answer => answer.ManualFeedback).HasMaxLength(2048);
        builder.HasIndex(answer => new { answer.SessionId, answer.QuestionId }).IsUnique();
    }
}

public sealed class BehavioralEventRecordConfiguration : IEntityTypeConfiguration<BehavioralEventRecord>
{
    public void Configure(EntityTypeBuilder<BehavioralEventRecord> builder)
    {
        builder.ToTable("behavioral_events");
        builder.HasKey(behavioralEvent => behavioralEvent.Id);
        builder.Property(behavioralEvent => behavioralEvent.Id).ValueGeneratedNever();
        builder.Property(behavioralEvent => behavioralEvent.Nonce).HasMaxLength(128).IsRequired();
        builder.Property(behavioralEvent => behavioralEvent.QuestionId);
        builder.Property(behavioralEvent => behavioralEvent.EventType).HasMaxLength(128).IsRequired();
        builder.Property(behavioralEvent => behavioralEvent.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(behavioralEvent => behavioralEvent.Signature).HasMaxLength(256).IsRequired();
        builder.Property(behavioralEvent => behavioralEvent.ValidationFlags).HasMaxLength(512).IsRequired();
        builder.HasIndex(behavioralEvent => new { behavioralEvent.SessionId, behavioralEvent.Seq }).IsUnique();
        builder.HasIndex(behavioralEvent => new { behavioralEvent.ExamScheduleId, behavioralEvent.EventType });
        builder.HasIndex(behavioralEvent => new { behavioralEvent.SessionId, behavioralEvent.QuestionId });
    }
}

public sealed class SessionFeatureAggregateConfiguration : IEntityTypeConfiguration<SessionFeatureAggregate>
{
    public void Configure(EntityTypeBuilder<SessionFeatureAggregate> builder)
    {
        builder.ToTable("session_feature_aggregates");
        builder.HasKey(aggregate => aggregate.Id);
        builder.Property(aggregate => aggregate.Id).ValueGeneratedNever();
        builder.Property(aggregate => aggregate.FeatureCountsJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(aggregate => aggregate.SessionId).IsUnique();
        builder.HasIndex(aggregate => aggregate.ExamScheduleId);
    }
}

public sealed class RiskScoreSnapshotConfiguration : IEntityTypeConfiguration<RiskScoreSnapshot>
{
    public void Configure(EntityTypeBuilder<RiskScoreSnapshot> builder)
    {
        builder.ToTable("risk_score_snapshots");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
        builder.Property(snapshot => snapshot.Reason).HasMaxLength(512).IsRequired();
        builder.HasIndex(snapshot => new { snapshot.SessionId, snapshot.CreatedAtUtc });
    }
}

public sealed class ScoringRuleConfiguration : IEntityTypeConfiguration<ScoringRule>
{
    public void Configure(EntityTypeBuilder<ScoringRule> builder)
    {
        builder.ToTable("scoring_rules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.FeatureKey).HasMaxLength(128).IsRequired();
        builder.HasIndex(rule => rule.FeatureKey).IsUnique();
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Type).HasMaxLength(128).IsRequired();
        builder.Property(message => message.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.Error).HasMaxLength(2048);
        builder.HasIndex(message => new { message.ProcessedAtUtc, message.OccurredAtUtc });
    }
}
