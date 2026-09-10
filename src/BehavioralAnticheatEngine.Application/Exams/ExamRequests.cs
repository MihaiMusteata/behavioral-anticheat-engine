using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Exams.Contracts;

namespace BehavioralAnticheatEngine.Application.Exams;

public sealed record ListAssessmentsQuery : IAppRequest<IReadOnlyCollection<AssessmentSummaryDto>>;

public sealed record GetAssessmentQuery(Guid AssessmentId) : IAppRequest<AssessmentDto>;

public sealed record CreateAssessmentCommand(Guid ActorUserId, CreateAssessmentRequest Request) : IAppRequest<AssessmentDto>;

public sealed record UpdateAssessmentCommand(Guid AssessmentId, UpdateAssessmentRequest Request) : IAppRequest<AssessmentDto>;

public sealed record DeleteAssessmentCommand(Guid AssessmentId) : IAppRequest<bool>;

public sealed record AddQuestionCommand(Guid AssessmentId, UpsertQuestionRequest Request) : IAppRequest<QuestionDto>;

public sealed record UpdateQuestionCommand(Guid QuestionId, UpsertQuestionRequest Request) : IAppRequest<QuestionDto>;

public sealed record DeleteQuestionCommand(Guid QuestionId) : IAppRequest<bool>;

public sealed record ListExamSchedulesQuery : IAppRequest<IReadOnlyCollection<ExamScheduleDto>>;

public sealed record CreateExamScheduleCommand(Guid ActorUserId, CreateExamScheduleRequest Request) : IAppRequest<ExamScheduleDto>;

public sealed record UpdateExamScheduleCommand(Guid ExamScheduleId, UpdateExamScheduleRequest Request) : IAppRequest<ExamScheduleDto>;

public sealed record DeleteExamScheduleCommand(Guid ExamScheduleId) : IAppRequest<bool>;

public sealed record StartScheduledExamCommand(Guid ExamScheduleId) : IAppRequest<ExamScheduleDto>;

public sealed record StopScheduledExamCommand(Guid ExamScheduleId) : IAppRequest<ExamScheduleDto>;

public sealed record JoinExamAsGuestCommand(JoinExamGuestRequest Request) : IAppRequest<GuestJoinResponse>;

public sealed record StartExamSessionCommand(Guid ExamScheduleId, Guid StudentId) : IAppRequest<StartExamSessionResponse>;

public sealed record SaveAnswerCommand(Guid SessionId, Guid StudentId, SaveAnswerRequest Request) : IAppRequest<SaveAnswerResponse>;

public sealed record SubmitExamCommand(Guid SessionId, Guid StudentId) : IAppRequest<SubmitExamResponse>;

public sealed record ManualScoreAnswerCommand(Guid SessionId, Guid QuestionId, ManualScoreRequest Request) : IAppRequest<SaveAnswerResponse>;

public sealed record SelfReportCheatingCommand(Guid SessionId, Guid StudentId, SelfReportCheatingRequest Request) : IAppRequest<bool>;

internal sealed class ListAssessmentsHandler : IAppRequestHandler<ListAssessmentsQuery, IReadOnlyCollection<AssessmentSummaryDto>>
{
    private readonly IExamManagementService _service;

    public ListAssessmentsHandler(IExamManagementService service) => _service = service;

    public Task<IReadOnlyCollection<AssessmentSummaryDto>> HandleAsync(ListAssessmentsQuery request, CancellationToken cancellationToken)
    {
        return _service.ListAssessmentsAsync(cancellationToken);
    }
}

internal sealed class GetAssessmentHandler : IAppRequestHandler<GetAssessmentQuery, AssessmentDto>
{
    private readonly IExamManagementService _service;

    public GetAssessmentHandler(IExamManagementService service) => _service = service;

    public Task<AssessmentDto> HandleAsync(GetAssessmentQuery request, CancellationToken cancellationToken)
    {
        return _service.GetAssessmentAsync(request.AssessmentId, cancellationToken);
    }
}

internal sealed class CreateAssessmentHandler : IAppRequestHandler<CreateAssessmentCommand, AssessmentDto>
{
    private readonly IExamManagementService _service;

    public CreateAssessmentHandler(IExamManagementService service) => _service = service;

    public Task<AssessmentDto> HandleAsync(CreateAssessmentCommand request, CancellationToken cancellationToken)
    {
        return _service.CreateAssessmentAsync(request.ActorUserId, request.Request, cancellationToken);
    }
}

internal sealed class UpdateAssessmentHandler : IAppRequestHandler<UpdateAssessmentCommand, AssessmentDto>
{
    private readonly IExamManagementService _service;

    public UpdateAssessmentHandler(IExamManagementService service) => _service = service;

    public Task<AssessmentDto> HandleAsync(UpdateAssessmentCommand request, CancellationToken cancellationToken)
    {
        return _service.UpdateAssessmentAsync(request.AssessmentId, request.Request, cancellationToken);
    }
}

internal sealed class DeleteAssessmentHandler : IAppRequestHandler<DeleteAssessmentCommand, bool>
{
    private readonly IExamManagementService _service;

    public DeleteAssessmentHandler(IExamManagementService service) => _service = service;

    public Task<bool> HandleAsync(DeleteAssessmentCommand request, CancellationToken cancellationToken)
    {
        return _service.DeleteAssessmentAsync(request.AssessmentId, cancellationToken);
    }
}

internal sealed class AddQuestionHandler : IAppRequestHandler<AddQuestionCommand, QuestionDto>
{
    private readonly IExamManagementService _service;

    public AddQuestionHandler(IExamManagementService service) => _service = service;

    public Task<QuestionDto> HandleAsync(AddQuestionCommand request, CancellationToken cancellationToken)
    {
        return _service.AddQuestionAsync(request.AssessmentId, request.Request, cancellationToken);
    }
}

internal sealed class UpdateQuestionHandler : IAppRequestHandler<UpdateQuestionCommand, QuestionDto>
{
    private readonly IExamManagementService _service;

    public UpdateQuestionHandler(IExamManagementService service) => _service = service;

    public Task<QuestionDto> HandleAsync(UpdateQuestionCommand request, CancellationToken cancellationToken)
    {
        return _service.UpdateQuestionAsync(request.QuestionId, request.Request, cancellationToken);
    }
}

internal sealed class DeleteQuestionHandler : IAppRequestHandler<DeleteQuestionCommand, bool>
{
    private readonly IExamManagementService _service;

    public DeleteQuestionHandler(IExamManagementService service) => _service = service;

    public Task<bool> HandleAsync(DeleteQuestionCommand request, CancellationToken cancellationToken)
    {
        return _service.DeleteQuestionAsync(request.QuestionId, cancellationToken);
    }
}

internal sealed class ListExamSchedulesHandler : IAppRequestHandler<ListExamSchedulesQuery, IReadOnlyCollection<ExamScheduleDto>>
{
    private readonly IExamManagementService _service;

    public ListExamSchedulesHandler(IExamManagementService service) => _service = service;

    public Task<IReadOnlyCollection<ExamScheduleDto>> HandleAsync(ListExamSchedulesQuery request, CancellationToken cancellationToken)
    {
        return _service.ListExamSchedulesAsync(cancellationToken);
    }
}

internal sealed class CreateExamScheduleHandler : IAppRequestHandler<CreateExamScheduleCommand, ExamScheduleDto>
{
    private readonly IExamManagementService _service;

    public CreateExamScheduleHandler(IExamManagementService service) => _service = service;

    public Task<ExamScheduleDto> HandleAsync(CreateExamScheduleCommand request, CancellationToken cancellationToken)
    {
        return _service.CreateExamScheduleAsync(request.ActorUserId, request.Request, cancellationToken);
    }
}

internal sealed class UpdateExamScheduleHandler : IAppRequestHandler<UpdateExamScheduleCommand, ExamScheduleDto>
{
    private readonly IExamManagementService _service;

    public UpdateExamScheduleHandler(IExamManagementService service) => _service = service;

    public Task<ExamScheduleDto> HandleAsync(UpdateExamScheduleCommand request, CancellationToken cancellationToken)
    {
        return _service.UpdateExamScheduleAsync(request.ExamScheduleId, request.Request, cancellationToken);
    }
}

internal sealed class DeleteExamScheduleHandler : IAppRequestHandler<DeleteExamScheduleCommand, bool>
{
    private readonly IExamManagementService _service;

    public DeleteExamScheduleHandler(IExamManagementService service) => _service = service;

    public Task<bool> HandleAsync(DeleteExamScheduleCommand request, CancellationToken cancellationToken)
    {
        return _service.DeleteExamScheduleAsync(request.ExamScheduleId, cancellationToken);
    }
}

internal sealed class StartScheduledExamHandler : IAppRequestHandler<StartScheduledExamCommand, ExamScheduleDto>
{
    private readonly IExamManagementService _service;

    public StartScheduledExamHandler(IExamManagementService service) => _service = service;

    public Task<ExamScheduleDto> HandleAsync(StartScheduledExamCommand request, CancellationToken cancellationToken)
    {
        return _service.StartScheduleAsync(request.ExamScheduleId, cancellationToken);
    }
}

internal sealed class StopScheduledExamHandler : IAppRequestHandler<StopScheduledExamCommand, ExamScheduleDto>
{
    private readonly IExamManagementService _service;

    public StopScheduledExamHandler(IExamManagementService service) => _service = service;

    public Task<ExamScheduleDto> HandleAsync(StopScheduledExamCommand request, CancellationToken cancellationToken)
    {
        return _service.StopScheduleAsync(request.ExamScheduleId, cancellationToken);
    }
}

internal sealed class JoinExamAsGuestHandler : IAppRequestHandler<JoinExamAsGuestCommand, GuestJoinResponse>
{
    private readonly IExamSessionService _service;

    public JoinExamAsGuestHandler(IExamSessionService service) => _service = service;

    public Task<GuestJoinResponse> HandleAsync(JoinExamAsGuestCommand request, CancellationToken cancellationToken)
    {
        return _service.JoinAsGuestAsync(request.Request, cancellationToken);
    }
}

internal sealed class StartExamSessionHandler : IAppRequestHandler<StartExamSessionCommand, StartExamSessionResponse>
{
    private readonly IExamSessionService _service;

    public StartExamSessionHandler(IExamSessionService service) => _service = service;

    public Task<StartExamSessionResponse> HandleAsync(StartExamSessionCommand request, CancellationToken cancellationToken)
    {
        return _service.StartSessionAsync(request.ExamScheduleId, request.StudentId, cancellationToken);
    }
}

internal sealed class SaveAnswerHandler : IAppRequestHandler<SaveAnswerCommand, SaveAnswerResponse>
{
    private readonly IExamSessionService _service;

    public SaveAnswerHandler(IExamSessionService service) => _service = service;

    public Task<SaveAnswerResponse> HandleAsync(SaveAnswerCommand request, CancellationToken cancellationToken)
    {
        return _service.SaveAnswerAsync(request.SessionId, request.StudentId, request.Request, cancellationToken);
    }
}

internal sealed class SubmitExamHandler : IAppRequestHandler<SubmitExamCommand, SubmitExamResponse>
{
    private readonly IExamSessionService _service;

    public SubmitExamHandler(IExamSessionService service) => _service = service;

    public Task<SubmitExamResponse> HandleAsync(SubmitExamCommand request, CancellationToken cancellationToken)
    {
        return _service.SubmitAsync(request.SessionId, request.StudentId, cancellationToken);
    }
}

internal sealed class ManualScoreAnswerHandler : IAppRequestHandler<ManualScoreAnswerCommand, SaveAnswerResponse>
{
    private readonly IExamSessionService _service;

    public ManualScoreAnswerHandler(IExamSessionService service) => _service = service;

    public Task<SaveAnswerResponse> HandleAsync(ManualScoreAnswerCommand request, CancellationToken cancellationToken)
    {
        return _service.ScoreAnswerManuallyAsync(request.SessionId, request.QuestionId, request.Request, cancellationToken);
    }
}

internal sealed class SelfReportCheatingHandler : IAppRequestHandler<SelfReportCheatingCommand, bool>
{
    private readonly IExamSessionService _service;

    public SelfReportCheatingHandler(IExamSessionService service) => _service = service;

    public Task<bool> HandleAsync(SelfReportCheatingCommand request, CancellationToken cancellationToken)
    {
        return _service.SelfReportCheatingAsync(request.SessionId, request.StudentId, request.Request, cancellationToken);
    }
}
