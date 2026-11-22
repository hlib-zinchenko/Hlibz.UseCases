using Atlas.Application.Cities;

using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Application;

public static class AtlasApplicationModule
{
    /// <summary>
    /// Registers Atlas's use cases, whichever host runs them: every handler wrapped in its
    /// pipeline (validation, the audit filter, the handler).
    /// </summary>
    public static IServiceCollection AddAtlasApplication(this IServiceCollection services)
    {
        services.AddSingleton<CityStore>();
        services.AddUseCases([typeof(AtlasApplicationModule).Assembly]);
        services.AddUseCaseFilter<AuditFilter>();
        return services;
    }
}
