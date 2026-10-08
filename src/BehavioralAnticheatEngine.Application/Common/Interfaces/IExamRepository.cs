using BehavioralAnticheatEngine.Domain.Exams;

namespace BehavioralAnticheatEngine.Application.Common.Interfaces;

public interface IExamRepository
{
    Task<List<Assessment>> ListAssessmentsAsync(CancellationToken cancellationToken = default);

    Task<Assessment?> GetAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default);

    Task<Assessment?> GetAssessmentWithQuestionsAsync(Guid assessmentId, CancellationToken cancellationToken = default);

    Task AddAssessmentAsync(Assessment assessment, CancellationToken cancellationToken = default);

    Task<Question?> GetQuestionWithOptionsAsync(Guid questionId, CancellationToken cancellationToken = default);

    Task AddQuestionAsync(Question question, CancellationToken cancellationToken = default);

    void RemoveQuestion(Question question);

    Task<List<ExamSchedule>> ListExamSchedulesAsync(CancellationToken cancellationToken = default);

    Task<ExamSchedule?> GetExamScheduleAsync(Guid examScheduleId, bool includeAssessment, CancellationToken cancellationToken = default);

    Task<ExamSchedule?> GetExamScheduleByPinCodeAsync(string pinCode, bool includeAssessment, CancellationToken cancellationToken = default);

    Task<bool> ExistsByAccessPinCodeAsync(string pinCode, CancellationToken cancellationToken = default);

    Task AddExamScheduleAsync(ExamSchedule examSchedule, CancellationToken cancellationToken = default);

    Task<ExamSession?> GetActiveSessionAsync(Guid examScheduleId, Guid studentId, CancellationToken cancellationToken = default);

    Task<ExamSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<ExamSession?> GetSessionWithAssessmentAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<List<ExamSession>> ListSessionsForExamAsync(Guid examScheduleId, CancellationToken cancellationToken = default);

    Task<List<ExamSession>> ListActiveSessionsAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    Task AddSessionAsync(ExamSession session, CancellationToken cancellationToken = default);

    Task<StudentAnswer?> GetAnswerAsync(Guid sessionId, Guid questionId, CancellationToken cancellationToken = default);

    Task<List<StudentAnswer>> ListAnswersForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task AddAnswerAsync(StudentAnswer answer, CancellationToken cancellationToken = default);
}
