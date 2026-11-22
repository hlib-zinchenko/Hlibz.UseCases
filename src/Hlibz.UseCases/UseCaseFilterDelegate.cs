namespace Hlibz.UseCases;

/// <summary>
/// The rest of a use case's pipeline, from a filter's point of view.
/// </summary>
/// <param name="context">The use case being handled.</param>
/// <returns>The handler's result, or a later filter's failure.</returns>
public delegate ValueTask<Result> UseCaseFilterDelegate(UseCaseContext context);
