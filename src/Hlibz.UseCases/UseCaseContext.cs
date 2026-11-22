namespace Hlibz.UseCases;

/// <summary>
/// The use case an <see cref="IUseCaseFilter"/> runs for.
/// </summary>
public sealed class UseCaseContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UseCaseContext"/> class.
    /// </summary>
    /// <param name="useCase">The use case.</param>
    /// <param name="useCaseType">The use case's declared type.</param>
    /// <param name="handlerType">The handler's type.</param>
    /// <param name="services">The scope's services.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    public UseCaseContext(
        IUseCase useCase,
        Type useCaseType,
        Type handlerType,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(useCaseType);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(services);

        UseCase = useCase;
        UseCaseType = useCaseType;
        HandlerType = handlerType;
        Services = services;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the use case, already validated.
    /// </summary>
    public IUseCase UseCase { get; }

    /// <summary>
    /// Gets the use case's declared type: the handler's <c>TUseCase</c>.
    /// </summary>
    public Type UseCaseType { get; }

    /// <summary>
    /// Gets the handler's type.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Gets the services of the scope the handler was resolved from.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the token that stops the work.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
