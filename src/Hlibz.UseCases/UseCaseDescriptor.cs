namespace Hlibz.UseCases;

/// <summary>
/// A registered use case.
/// </summary>
/// <param name="UseCaseType">The use case type.</param>
/// <param name="HandlerType">Its handler's type.</param>
/// <param name="ResultType">
/// The handler's <c>TResult</c>, or <see langword="null"/> when it returns no value.
/// </param>
/// <param name="CanFailValidation">
/// Whether the use case has any rule to break, so its pipeline can answer with a validation
/// failure.
/// </param>
public sealed record UseCaseDescriptor(
    Type UseCaseType,
    Type HandlerType,
    Type? ResultType,
    bool CanFailValidation);
