using BehavioralAnticheatEngine.Application.Behavior.Contracts;
using BehavioralAnticheatEngine.Application.Common.Mediation;

namespace BehavioralAnticheatEngine.Application.Behavior;

public sealed record IngestBehavioralEventsCommand(Guid StudentId, BehavioralEventBatchRequest Request)
    : IAppRequest<BehavioralEventBatchResult>;

internal sealed class IngestBehavioralEventsHandler : IAppRequestHandler<IngestBehavioralEventsCommand, BehavioralEventBatchResult>
{
    private readonly IBehavioralIngestionService _service;

    public IngestBehavioralEventsHandler(IBehavioralIngestionService service)
    {
        _service = service;
    }

    public Task<BehavioralEventBatchResult> HandleAsync(IngestBehavioralEventsCommand request, CancellationToken cancellationToken)
    {
        return _service.IngestAsync(request.StudentId, request.Request, cancellationToken);
    }
}
