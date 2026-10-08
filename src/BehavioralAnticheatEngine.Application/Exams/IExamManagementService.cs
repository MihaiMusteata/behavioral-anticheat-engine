using BehavioralAnticheatEngine.Application.Exams.Contracts;

namespace BehavioralAnticheatEngine.Application.Exams;

public interface IExamManagementService
{
    Task<IReadOnlyCollection<AssessmentSummaryDto>> ListAssessmentsAsync(CancellationToken cancellationToken = default);

    Task<AssessmentDto> GetAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default);

    Task<AssessmentDto> CreateAssessmentAsync(Guid actorUserId, CreateAssessmentRequest request, CancellationToken cancellationToken = default);

    Task<AssessmentDto> UpdateAssessmentAsync(Guid assessmentId, UpdateAssessmentRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default);

    Task<QuestionDto> AddQuestionAsync(Guid assessmentId, UpsertQuestionRequest request, CancellationToken cancellationToken = default);

    Task<QuestionDto> UpdateQuestionAsync(Guid questionId, UpsertQuestionRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ExamScheduleDto>> ListExamSchedulesAsync(CancellationToken cancellationToken = default);

    Task<ExamScheduleDto> CreateExamScheduleAsync(Guid actorUserId, CreateExamScheduleRequest request, CancellationToken cancellationToken = default);

    Task<ExamScheduleDto> UpdateExamScheduleAsync(Guid examScheduleId, UpdateExamScheduleRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteExamScheduleAsync(Guid examScheduleId, CancellationToken cancellationToken = default);

    Task<ExamScheduleDto> StartScheduleAsync(Guid examScheduleId, CancellationToken cancellationToken = default);

    Task<ExamScheduleDto> StopScheduleAsync(Guid examScheduleId, CancellationToken cancellationToken = default);
}
