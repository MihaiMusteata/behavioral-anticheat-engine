using System.Reflection;
using BehavioralAnticheatEngine.Application.Behavior;
using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Dashboard;
using BehavioralAnticheatEngine.Application.Exams;
using Microsoft.Extensions.DependencyInjection;

namespace BehavioralAnticheatEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAppMediator, AppMediator>();
        services.AddScoped<IExamManagementService, ExamManagementService>();
        services.AddScoped<IExamSessionService, ExamSessionService>();
        services.AddScoped<IBehavioralIngestionService, BehavioralIngestionService>();
        services.AddScoped<IBehavioralEventProcessingService, BehavioralEventProcessingService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRiskScoringService, RuleBasedRiskScoringService>();
        services.AddScoped<IBehavioralEventProcessorRegistry, BehavioralEventProcessorRegistry>();

        foreach (var processorType in Assembly.GetExecutingAssembly()
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                typeof(IBehavioralEventProcessor).IsAssignableFrom(type.AsType())))
        {
            services.AddScoped(typeof(IBehavioralEventProcessor), processorType.AsType());
        }

        var handlerTypes = Assembly.GetExecutingAssembly()
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Select(type => new
            {
                Implementation = type.AsType(),
                Services = type.ImplementedInterfaces
                    .Where(iface => iface.IsGenericType &&
                        iface.GetGenericTypeDefinition() == typeof(IAppRequestHandler<,>))
                    .ToArray()
            })
            .Where(candidate => candidate.Services.Length > 0);

        foreach (var handlerType in handlerTypes)
        {
            foreach (var serviceType in handlerType.Services)
            {
                services.AddScoped(serviceType, handlerType.Implementation);
            }
        }

        return services;
    }
}
