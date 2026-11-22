namespace Hlibz.UseCases;

/// <summary>
/// Marks a record as a use case: the input one <see cref="IUseCaseHandler{TUseCase}"/> or
/// <see cref="IUseCaseHandler{TUseCase, TResult}"/> handles. It carries ids and plain values,
/// and the DataAnnotations rules every client must respect.
/// </summary>
/// <remarks>
/// The result type lives on the handler, not here. Before the handler runs, the generated
/// pipeline validates the use case against its validation attributes (on properties and record
/// constructor parameters, nested records and collection items included) and
/// <see cref="IValidatableObject"/>.
/// </remarks>
/// <example>
/// <code>
/// public sealed record CreateCityCommand(
///     [Required, MaxLength(100)] string Name,
///     [Range(1, 1000)] int MagnitudeKm) : IUseCase;
/// </code>
/// </example>
public interface IUseCase;
