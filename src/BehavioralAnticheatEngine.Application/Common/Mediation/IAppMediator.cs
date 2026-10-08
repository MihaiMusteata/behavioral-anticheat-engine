namespace BehavioralAnticheatEngine.Application.Common.Mediation;

public interface IAppMediator
{
    Task<TResponse> SendAsync<TResponse>(IAppRequest<TResponse> request, CancellationToken cancellationToken = default);
}
