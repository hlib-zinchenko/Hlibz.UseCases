namespace Hlibz.UseCases;

/// <summary>
/// Handles a use case that returns no value.
/// </summary>
/// <typeparam name="TUseCase">The use case.</typeparam>
/// <remarks>
/// Declare an empty interface deriving from this one for each handler
/// (<c>public interface IDeleteCityCommandHandler : IUseCaseHandler&lt;DeleteCityCommand&gt;;</c>)
/// and inject that in clients. The source generator registers the handler wrapped in its pipeline
/// under that interface, this one and <see cref="IUseCaseHandler"/>.
/// </remarks>
public interface IUseCaseHandler<in TUseCase> : IUseCaseHandler
    where TUseCase : IUseCase
{
    /// <summary>
    /// Handles <paramref name="useCase"/>.
    /// </summary>
    /// <param name="useCase">The use case.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Success, or the failure: an expected one is a result, never an exception.</returns>
    Task<Result> HandleAsync(TUseCase useCase, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles a use case that returns a value.
/// </summary>
/// <typeparam name="TUseCase">The use case.</typeparam>
/// <typeparam name="TResult">
/// What it returns: a DTO, a collection or page of DTOs, or a primitive, never a data model.
/// </typeparam>
/// <remarks>
/// Declare an empty interface deriving from this one for each handler
/// (<c>public interface ICreateCityCommandHandler : IUseCaseHandler&lt;CreateCityCommand, CityDto&gt;;</c>)
/// and inject that in clients. The source generator registers the handler wrapped in its pipeline
/// under that interface, this one and <see cref="IUseCaseHandler"/>.
/// </remarks>
public interface IUseCaseHandler<in TUseCase, TResult> : IUseCaseHandler
    where TUseCase : IUseCase
{
    /// <summary>
    /// Handles <paramref name="useCase"/>.
    /// </summary>
    /// <param name="useCase">The use case.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The value, or the failure: an expected one is a result, never an exception.</returns>
    Task<Result<TResult>> HandleAsync(
        TUseCase useCase,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Marks every use case handler. Each one is also registered under this interface, so a test can
/// resolve them all and catch one whose dependencies are missing.
/// </summary>
public interface IUseCaseHandler;
