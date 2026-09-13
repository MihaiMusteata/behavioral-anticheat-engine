using BehavioralAnticheatEngine.Application.Dashboard.Contracts;

namespace BehavioralAnticheatEngine.Application.Dashboard;

public interface IDashboardService
{
    Task<ExamDashboardDto> GetExamDashboardAsync(Guid examScheduleId, CancellationToken cancellationToken = default);

    Task<SessionTimelineDto> GetSessionTimelineAsync(Guid sessionId, Guid? questionId, CancellationToken cancellationToken = default);

    Task<SessionTimelineExportDto> ExportSessionTimelineAsync(Guid sessionId, Guid? questionId, string? eventType, CancellationToken cancellationToken = default);
}
