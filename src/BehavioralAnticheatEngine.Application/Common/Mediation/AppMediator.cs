using Microsoft.Extensions.DependencyInjection;

namespace BehavioralAnticheatEngine.Application.Common.Mediation;

public sealed class AppMediator : IAppMediator
{
    private readonly IServiceProvider _serviceProvider;

    public AppMediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResponse> SendAsync<TResponse>(IAppRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IAppRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        dynamic handler = _serviceProvider.GetRequiredService(handlerType);

        return handler.HandleAsync((dynamic)request, cancellationToken);
    }
}
