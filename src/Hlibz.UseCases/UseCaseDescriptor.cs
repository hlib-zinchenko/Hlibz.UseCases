namespace Hlibz.UseCases;

/// <summary>
/// A registered use case.
/// </summary>
/// <param name="UseCaseType">The use case type.</param>
/// <param name="HandlerType">Its handler's type.</param>
/// <param name="ResultType">
/// The handler's <c>TResult</c> (<see cref="Unit"/> for a handler declaring its errors but
/// returning no value), or <see langword="null"/> when it returns a plain <see cref="Result"/>.
/// </param>
/// <param name="ErrorSetType">
/// The handler's <c>TError</c>, the <see langword="union"/> of the errors it declares
/// (<see cref="ErrorSetDescriptor.For(Type)"/> lists them), or <see langword="null"/> when it
/// doesn't declare them.
/// </param>
/// <param name="CanFailValidation">
/// Whether the use case has any rule to break, so its pipeline can answer with
/// <see cref="ValidationFailed"/>.
/// </param>
public sealed record UseCaseDescriptor(
    Type UseCaseType,
    Type HandlerType,
    Type? ResultType,
    Type? ErrorSetType,
    bool CanFailValidation);
