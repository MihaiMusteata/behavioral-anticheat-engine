namespace BehavioralAnticheatEngine.Application.Common.Mediation;

public interface IAppRequestHandler<in TRequest, TResponse>
    where TRequest : IAppRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
