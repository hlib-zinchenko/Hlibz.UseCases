using Hlibz.UseCases.Internal;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hlibz.UseCases;

/// <summary>
/// Registers use case handlers and filters.
/// </summary>
public static class UseCaseServiceCollectionExtensions
{
    private const string InvalidOptionsMessage =
        "UseCaseOptions.Validation.FailureStatus must be Invalid or Error, and MaxDepth greater "
        + "than 0.";

    /// <summary>
    /// Registers every use case handler in <paramref name="assemblies"/>, each wrapped in its
    /// pipeline (validation, then the filters, then the handler) and registered scoped under its
    /// own interface, <see cref="IUseCaseHandler{TUseCase, TResult}"/> (or
    /// <see cref="IUseCaseHandler{TUseCase}"/>) and <see cref="IUseCaseHandler"/>. Also registers
    /// <see cref="IUseCaseCatalog"/> and <see cref="IUseCaseValidator"/>.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assemblies">
    /// The assemblies holding handlers. Calling this again (once per module, say) adds more; an
    /// assembly already added is skipped.
    /// </param>
    /// <param name="configure">Configures <see cref="UseCaseOptions"/>; may be called again.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// An assembly wasn't compiled with the package's source generator, or two assemblies hold a
    /// handler for the same use case.
    /// </exception>
    /// <remarks>
    /// Nothing is scanned: the source generator lists each assembly's handlers when it compiles,
    /// and reports a handler it can't register as a build error.
    /// </remarks>
    public static IServiceCollection AddUseCases(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        Action<UseCaseOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        UseCaseRegistry registry = RegistryOf(services);
        foreach (Assembly assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            foreach (UseCaseRegistration registration in registry.Add(assembly))
            {
                registration.Register(services);
            }
        }

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton(static provider => new ValidationEngine(
            provider.GetRequiredService<UseCaseRegistry>(),
            provider.GetRequiredService<IOptions<UseCaseOptions>>().Value.Validation));
        services.TryAddScoped<IUseCaseValidator>(static provider => new UseCaseValidator(
            provider.GetRequiredService<ValidationEngine>(),
            provider));
        services.TryAddScoped<UseCasePipeline>();
        return services;
    }

    /// <summary>
    /// Registers a filter every use case runs through, after validation and before its handler,
    /// in the order filters are registered. Scoped, so it can depend on the scope's services.
    /// </summary>
    /// <typeparam name="TFilter">The filter.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddUseCaseFilter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFilter>(
        this IServiceCollection services)
        where TFilter : class, IUseCaseFilter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IUseCaseFilter, TFilter>();
        return services;
    }

    private static bool IsValid(UseCaseOptions options) =>
        options.Validation.FailureStatus is ResultStatus.Invalid or ResultStatus.Error
        && options.Validation.MaxDepth > 0;

    /// <summary>
    /// The registry this service collection's earlier calls filled, or a new one, registered with
    /// what is registered only once. It is a singleton instance, so it is found among the
    /// descriptors rather than resolved.
    /// </summary>
    private static UseCaseRegistry RegistryOf(IServiceCollection services)
    {
        if (services.FirstOrDefault(static descriptor => descriptor.ServiceType == typeof(UseCaseRegistry))
                ?.ImplementationInstance is UseCaseRegistry existing)
        {
            return existing;
        }

        UseCaseRegistry registry = new();
        services.AddSingleton(registry);
        services.AddOptions<UseCaseOptions>().Validate(IsValid, InvalidOptionsMessage);
        services.AddSingleton<IUseCaseCatalog>(registry);
        return registry;
    }
}
