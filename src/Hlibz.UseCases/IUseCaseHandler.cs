namespace Hlibz.UseCases;

/// <summary>
/// Handles a use case that returns no value and doesn't declare its errors. To declare them,
/// implement <see cref="IUseCaseHandler{TUseCase, TResult, TError}"/> with <see cref="Unit"/>.
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
/// Handles a use case that returns a value and doesn't declare its errors: it can fail with any
/// <see cref="Error"/>. To declare them, implement
/// <see cref="IUseCaseHandler{TUseCase, TResult, TError}"/>.
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
/// Handles a use case that declares the errors it fails with: <typeparamref name="TError"/>, an
/// error set (a <see langword="union"/> of Hlibz.Results error types). Clients can switch over
/// <see cref="Result{T, TError}.DeclaredError"/> exhaustively, and documentation can list every
/// error with its data.
/// </summary>
/// <typeparam name="TUseCase">The use case.</typeparam>
/// <typeparam name="TResult">
/// What it returns: a DTO, a collection or page of DTOs, or a primitive, never a data model;
/// <see cref="Unit"/> for nothing.
/// </typeparam>
/// <typeparam name="TError">The error set.</typeparam>
/// <remarks>
/// <para>
/// Besides its own errors, the result can carry an <see cref="AmbientError"/>: the pipeline fails
/// an invalid use case with <see cref="ValidationFailed"/> before the handler runs, and a filter
/// can stop with <see cref="Unauthorized"/>, <see cref="Forbidden"/> or
/// <see cref="Unavailable"/>.
/// </para>
/// <para>
/// Declare an empty interface deriving from this one for each handler
/// (<c>public interface IDeleteCityCommandHandler : IUseCaseHandler&lt;DeleteCityCommand, Unit, DeleteCityError&gt;;</c>)
/// and inject that in clients. The source generator registers the handler wrapped in its pipeline
/// under that interface, this one and <see cref="IUseCaseHandler"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Error("cities_1001", ResultStatus.NotFound, "The city doesn't exist.")]
/// public sealed record CityNotFound(int CityId) : Error;
///
/// [Error("cities_1002", ResultStatus.Conflict, "The city is used by user profiles.")]
/// public sealed record CityInUse(IReadOnlyList&lt;int&gt; UserIds) : Error;
///
/// public union DeleteCityError(CityNotFound, CityInUse);
///
/// internal sealed class DeleteCityCommandHandler(CityStore store) : IDeleteCityCommandHandler
/// {
///     public async Task&lt;Result&lt;Unit, DeleteCityError&gt;&gt; HandleAsync(
///         DeleteCityCommand useCase,
///         CancellationToken cancellationToken = default)
///     {
///         if (await store.FindAsync(useCase.Id, cancellationToken) is null)
///         {
///             return new CityNotFound(useCase.Id);
///         }
///
///         IReadOnlyList&lt;int&gt; users = await store.UsersOfAsync(useCase.Id, cancellationToken);
///         return users.Count > 0 ? new CityInUse(users) : Unit.Value;
///     }
/// }
/// </code>
/// </example>
public interface IUseCaseHandler<in TUseCase, TResult, TError> : IUseCaseHandler
    where TUseCase : IUseCase
    where TError : struct, IUnion
{
    /// <summary>
    /// Handles <paramref name="useCase"/>.
    /// </summary>
    /// <param name="useCase">The use case.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>
    /// The value, or the failure: one of the set's errors, or an ambient one. An expected failure
    /// is a result, never an exception.
    /// </returns>
    Task<Result<TResult, TError>> HandleAsync(
        TUseCase useCase,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Marks every use case handler. Each one is also registered under this interface, so a test can
/// resolve them all and catch one whose dependencies are missing.
/// </summary>
public interface IUseCaseHandler;
