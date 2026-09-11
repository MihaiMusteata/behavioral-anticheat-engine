using BehavioralAnticheatEngine.Application.Exams.Contracts;

namespace BehavioralAnticheatEngine.Application.Exams;

public interface IExamSessionService
{
    Task<GuestJoinResponse> JoinAsGuestAsync(JoinExamGuestRequest request, CancellationToken cancellationToken = default);

    Task<StartExamSessionResponse> StartSessionAsync(Guid examScheduleId, Guid studentId, CancellationToken cancellationToken = default);

    Task<SaveAnswerResponse> SaveAnswerAsync(Guid sessionId, Guid studentId, SaveAnswerRequest request, CancellationToken cancellationToken = default);

    Task<SubmitExamResponse> SubmitAsync(Guid sessionId, Guid studentId, CancellationToken cancellationToken = default);

    Task<SaveAnswerResponse> ScoreAnswerManuallyAsync(Guid sessionId, Guid questionId, ManualScoreRequest request, CancellationToken cancellationToken = default);

    Task<bool> SelfReportCheatingAsync(Guid sessionId, Guid studentId, SelfReportCheatingRequest request, CancellationToken cancellationToken = default);
}
