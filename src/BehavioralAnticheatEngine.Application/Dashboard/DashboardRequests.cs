using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Dashboard.Contracts;

namespace BehavioralAnticheatEngine.Application.Dashboard;

public sealed record GetExamDashboardQuery(Guid ExamScheduleId) : IAppRequest<ExamDashboardDto>;

public sealed record GetSessionTimelineQuery(Guid SessionId, Guid? QuestionId) : IAppRequest<SessionTimelineDto>;

public sealed record ExportSessionTimelineQuery(Guid SessionId, Guid? QuestionId, string? EventType) : IAppRequest<SessionTimelineExportDto>;

internal sealed class GetExamDashboardHandler : IAppRequestHandler<GetExamDashboardQuery, ExamDashboardDto>
{
    private readonly IDashboardService _service;

    public GetExamDashboardHandler(IDashboardService service)
    {
        _service = service;
    }

    public Task<ExamDashboardDto> HandleAsync(GetExamDashboardQuery request, CancellationToken cancellationToken)
    {
        return _service.GetExamDashboardAsync(request.ExamScheduleId, cancellationToken);
    }
}

internal sealed class GetSessionTimelineHandler : IAppRequestHandler<GetSessionTimelineQuery, SessionTimelineDto>
{
    private readonly IDashboardService _service;

    public GetSessionTimelineHandler(IDashboardService service)
    {
        _service = service;
    }

    public Task<SessionTimelineDto> HandleAsync(GetSessionTimelineQuery request, CancellationToken cancellationToken)
    {
        return _service.GetSessionTimelineAsync(request.SessionId, request.QuestionId, cancellationToken);
    }
}

internal sealed class ExportSessionTimelineHandler : IAppRequestHandler<ExportSessionTimelineQuery, SessionTimelineExportDto>
{
    private readonly IDashboardService _service;

    public ExportSessionTimelineHandler(IDashboardService service)
    {
        _service = service;
    }

    public Task<SessionTimelineExportDto> HandleAsync(ExportSessionTimelineQuery request, CancellationToken cancellationToken)
    {
        return _service.ExportSessionTimelineAsync(request.SessionId, request.QuestionId, request.EventType, cancellationToken);
    }
}
