using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Repositories;

public sealed class ExamRepository : IExamRepository
{
    private readonly AppDbContext _dbContext;

    public ExamRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<Assessment>> ListAssessmentsAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Assessments
            .AsNoTracking()
            .Where(assessment => !assessment.IsDeleted)
            .Include(assessment => assessment.Questions)
            .OrderBy(assessment => assessment.Title)
            .ToListAsync(cancellationToken);
    }

    public Task<Assessment?> GetAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Assessments
            .FirstOrDefaultAsync(assessment => assessment.Id == assessmentId && !assessment.IsDeleted, cancellationToken);
    }

    public Task<Assessment?> GetAssessmentWithQuestionsAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Assessments
            .Include(assessment => assessment.Questions)
            .ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(assessment => assessment.Id == assessmentId && !assessment.IsDeleted, cancellationToken);
    }

    public async Task AddAssessmentAsync(Assessment assessment, CancellationToken cancellationToken = default)
    {
        await _dbContext.Assessments.AddAsync(assessment, cancellationToken);
    }

    public Task<Question?> GetQuestionWithOptionsAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Questions
            .Include(question => question.Options)
            .FirstOrDefaultAsync(question => question.Id == questionId, cancellationToken);
    }

    public async Task AddQuestionAsync(Question question, CancellationToken cancellationToken = default)
    {
        await _dbContext.Questions.AddAsync(question, cancellationToken);
    }

    public void RemoveQuestion(Question question)
    {
        _dbContext.Questions.Remove(question);
    }

    public Task<List<ExamSchedule>> ListExamSchedulesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.ExamSchedules
            .AsNoTracking()
            .Where(schedule => !schedule.IsDeleted)
            .Include(schedule => schedule.Assessment)
            .OrderByDescending(schedule => schedule.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<ExamSchedule?> GetExamScheduleAsync(
        Guid examScheduleId,
        bool includeAssessment,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ExamSchedule> query = _dbContext.ExamSchedules.Where(schedule => !schedule.IsDeleted);

        if (includeAssessment)
        {
            query = query.Include(schedule => schedule.Assessment)
                .ThenInclude(assessment => assessment!.Questions)
                .ThenInclude(question => question.Options);
        }

        return query.FirstOrDefaultAsync(schedule => schedule.Id == examScheduleId, cancellationToken);
    }

    public Task<ExamSchedule?> GetExamScheduleByPinCodeAsync(
        string pinCode,
        bool includeAssessment,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ExamSchedule> query = _dbContext.ExamSchedules.Where(schedule => !schedule.IsDeleted);

        if (includeAssessment)
        {
            query = query.Include(schedule => schedule.Assessment)
                .ThenInclude(assessment => assessment!.Questions)
                .ThenInclude(question => question.Options);
        }

        return query.FirstOrDefaultAsync(schedule => schedule.AccessPinCode == pinCode, cancellationToken);
    }

    public Task<bool> ExistsByAccessPinCodeAsync(string pinCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExamSchedules.AnyAsync(schedule => schedule.AccessPinCode == pinCode, cancellationToken);
    }

    public async Task AddExamScheduleAsync(ExamSchedule examSchedule, CancellationToken cancellationToken = default)
    {
        await _dbContext.ExamSchedules.AddAsync(examSchedule, cancellationToken);
    }

    public Task<ExamSession?> GetActiveSessionAsync(Guid examScheduleId, Guid studentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExamSessions
            .FirstOrDefaultAsync(
                session => session.ExamScheduleId == examScheduleId &&
                    session.StudentId == studentId &&
                    session.Status == ExamSessionStatuses.Active,
                cancellationToken);
    }

    public Task<ExamSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExamSessions.FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
    }

    public Task<ExamSession?> GetSessionWithAssessmentAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExamSessions
            .Include(session => session.Assessment)
            .ThenInclude(assessment => assessment!.Questions)
            .ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
    }

    public Task<List<ExamSession>> ListSessionsForExamAsync(Guid examScheduleId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExamSessions
            .AsNoTracking()
            .Where(session => session.ExamScheduleId == examScheduleId)
            .OrderBy(session => session.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ExamSession>> ListActiveSessionsAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExamSessions
            .AsNoTracking()
            .Where(session => session.Status == ExamSessionStatuses.Active && session.EndsAtUtc > nowUtc)
            .OrderBy(session => session.LastActivityAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSessionAsync(ExamSession session, CancellationToken cancellationToken = default)
    {
        await _dbContext.ExamSessions.AddAsync(session, cancellationToken);
    }

    public Task<StudentAnswer?> GetAnswerAsync(Guid sessionId, Guid questionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.StudentAnswers
            .FirstOrDefaultAsync(answer => answer.SessionId == sessionId && answer.QuestionId == questionId, cancellationToken);
    }

    public Task<List<StudentAnswer>> ListAnswersForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.StudentAnswers
            .Where(answer => answer.SessionId == sessionId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAnswerAsync(StudentAnswer answer, CancellationToken cancellationToken = default)
    {
        await _dbContext.StudentAnswers.AddAsync(answer, cancellationToken);
    }
}
