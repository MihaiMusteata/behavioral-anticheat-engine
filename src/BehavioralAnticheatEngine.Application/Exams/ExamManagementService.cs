using BehavioralAnticheatEngine.Application.Common.Exceptions;
using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Exams.Contracts;
using BehavioralAnticheatEngine.Domain.Exams;

namespace BehavioralAnticheatEngine.Application.Exams;

public sealed class ExamManagementService : IExamManagementService
{
    private readonly IExamRepository _exams;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ExamManagementService(IExamRepository exams, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _exams = exams;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyCollection<AssessmentSummaryDto>> ListAssessmentsAsync(CancellationToken cancellationToken = default)
    {
        var assessments = await _exams.ListAssessmentsAsync(cancellationToken);

        return assessments.Select(ToSummaryDto).ToArray();
    }

    public async Task<AssessmentDto> GetAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await _exams.GetAssessmentWithQuestionsAsync(assessmentId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Assessment was not found.");

        return ToDto(assessment);
    }

    public async Task<AssessmentDto> CreateAssessmentAsync(Guid actorUserId, CreateAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        var assessment = Assessment.Create(
            request.Title,
            request.Description,
            actorUserId,
            _timeProvider.GetUtcNow());

        if (request.Publish)
        {
            assessment.Publish();
        }

        await _exams.AddAssessmentAsync(assessment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(assessment);
    }

    public async Task<AssessmentDto> UpdateAssessmentAsync(Guid assessmentId, UpdateAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        var assessment = await _exams.GetAssessmentWithQuestionsAsync(assessmentId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Assessment was not found.");

        assessment.Update(request.Title, request.Description, _timeProvider.GetUtcNow());

        if (request.Publish)
        {
            assessment.Publish();
        }
        else
        {
            assessment.Unpublish();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(assessment);
    }

    public async Task<bool> DeleteAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await _exams.GetAssessmentAsync(assessmentId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Assessment was not found.");

        assessment.Delete(_timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<QuestionDto> AddQuestionAsync(Guid assessmentId, UpsertQuestionRequest request, CancellationToken cancellationToken = default)
    {
        _ = await _exams.GetAssessmentAsync(assessmentId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Assessment was not found.");

        var question = Question.Create(
            assessmentId,
            request.Type,
            request.Prompt,
            request.Points,
            request.OrderIndex,
            request.Options.Select(option => new QuestionOptionInput(option.Label, option.IsCorrect, option.OrderIndex)));

        await _exams.AddQuestionAsync(question, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(question);
    }

    public async Task<QuestionDto> UpdateQuestionAsync(Guid questionId, UpsertQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var question = await _exams.GetQuestionWithOptionsAsync(questionId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Question was not found.");

        question.Update(
            request.Type,
            request.Prompt,
            request.Points,
            request.OrderIndex,
            request.Options.Select(option => new QuestionOptionInput(option.Label, option.IsCorrect, option.OrderIndex)));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(question);
    }

    public async Task<bool> DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var question = await _exams.GetQuestionWithOptionsAsync(questionId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Question was not found.");

        _exams.RemoveQuestion(question);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyCollection<ExamScheduleDto>> ListExamSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await _exams.ListExamSchedulesAsync(cancellationToken);

        return schedules.Select(ToDto).ToArray();
    }

    public async Task<ExamScheduleDto> CreateExamScheduleAsync(Guid actorUserId, CreateExamScheduleRequest request, CancellationToken cancellationToken = default)
    {
        _ = await _exams.GetAssessmentAsync(request.AssessmentId, cancellationToken)
            ?? throw new ApplicationNotFoundException("Assessment was not found.");

        var schedule = ExamSchedule.Create(
            request.AssessmentId,
            request.Name,
            request.DurationMinutes,
            actorUserId,
            _timeProvider.GetUtcNow());

        await _exams.AddExamScheduleAsync(schedule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        schedule = await _exams.GetExamScheduleAsync(schedule.Id, includeAssessment: true, cancellationToken)
            ?? schedule;

        return ToDto(schedule);
    }

    public async Task<ExamScheduleDto> UpdateExamScheduleAsync(Guid examScheduleId, UpdateExamScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var schedule = await _exams.GetExamScheduleAsync(examScheduleId, includeAssessment: true, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam schedule was not found.");

        schedule.Update(request.Name, request.DurationMinutes);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(schedule);
    }

    public async Task<bool> DeleteExamScheduleAsync(Guid examScheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _exams.GetExamScheduleAsync(examScheduleId, includeAssessment: false, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam schedule was not found.");

        schedule.Delete(_timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<ExamScheduleDto> StartScheduleAsync(Guid examScheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _exams.GetExamScheduleAsync(examScheduleId, includeAssessment: true, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam schedule was not found.");

        if (string.IsNullOrEmpty(schedule.AccessPinCode))
        {
            var pinCode = await GenerateUniquePinCodeAsync(cancellationToken);
            schedule.AssignAccessPinCode(pinCode);
        }

        schedule.Start(_timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(schedule);
    }

    public async Task<ExamScheduleDto> StopScheduleAsync(Guid examScheduleId, CancellationToken cancellationToken = default)
    {
        var schedule = await _exams.GetExamScheduleAsync(examScheduleId, includeAssessment: true, cancellationToken)
            ?? throw new ApplicationNotFoundException("Exam schedule was not found.");

        schedule.Stop(_timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(schedule);
    }

    private async Task<string> GenerateUniquePinCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = ExamSchedule.GeneratePinCode();
            if (!await _exams.ExistsByAccessPinCodeAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new ApplicationConflictException("Could not generate a unique access code. Please try again.");
    }

    private static AssessmentSummaryDto ToSummaryDto(Assessment assessment)
    {
        return new AssessmentSummaryDto(
            assessment.Id,
            assessment.Title,
            assessment.Description,
            assessment.IsPublished,
            assessment.Questions.Count);
    }

    public static AssessmentDto ToDto(Assessment assessment)
    {
        return new AssessmentDto(
            assessment.Id,
            assessment.Title,
            assessment.Description,
            assessment.IsPublished,
            assessment.Questions.OrderBy(question => question.OrderIndex).Select(ToDto).ToArray());
    }

    public static QuestionDto ToDto(Question question)
    {
        return new QuestionDto(
            question.Id,
            question.Type,
            question.Prompt,
            question.Points,
            question.OrderIndex,
            question.Options.OrderBy(option => option.OrderIndex).Select(ToDto).ToArray());
    }

    private static QuestionOptionDto ToDto(QuestionOption option)
    {
        return new QuestionOptionDto(option.Id, option.Label, option.IsCorrect, option.OrderIndex);
    }

    private static ExamScheduleDto ToDto(ExamSchedule schedule)
    {
        return new ExamScheduleDto(
            schedule.Id,
            schedule.AssessmentId,
            schedule.Assessment?.Title ?? string.Empty,
            schedule.Name,
            schedule.DurationMinutes,
            schedule.StartsAtUtc,
            schedule.EndsAtUtc,
            schedule.Status,
            schedule.AccessPinCode ?? string.Empty);
    }
}
