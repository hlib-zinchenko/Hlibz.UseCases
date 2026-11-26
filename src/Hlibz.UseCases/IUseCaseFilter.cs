namespace Hlibz.UseCases;

/// <summary>
/// A step every use case runs through after validation and before its handler: authorization,
/// logging, a transaction. Registered with
/// <see cref="UseCaseServiceCollectionExtensions.AddUseCaseFilter{TFilter}"/>; filters run in the
/// order they are registered.
/// </summary>
/// <remarks>
/// A filter sees the plain <see cref="Result"/>. It continues by calling <c>next</c> (and may
/// look at what comes back), or stops by returning a failure of its own, which the pipeline turns
/// into the handler's result type. That failure is an <see cref="AmbientError"/>
/// (<see cref="Unauthorized"/>, <see cref="Forbidden"/>, <see cref="Unavailable"/>), since a
/// handler that declares its errors accepts no other: any other error from a filter makes the
/// pipeline throw for such a handler. Returning a success without calling <c>next</c> is a bug
/// too: the handler's value would be missing.
/// </remarks>
/// <example>
/// <code>
/// internal sealed class AdminOnlyFilter(IUserContext user) : IUseCaseFilter
/// {
///     public ValueTask&lt;Result&gt; InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next) =>
///         context.UseCaseType.IsDefined(typeof(AdminOnlyAttribute)) &amp;&amp; !user.IsAdmin
///             ? ValueTask.FromResult&lt;Result&gt;(new Forbidden())
///             : next(context);
/// }
/// </code>
/// </example>
public interface IUseCaseFilter
{
    /// <summary>
    /// Runs the filter.
    /// </summary>
    /// <param name="context">The use case being handled.</param>
    /// <param name="next">The rest of the pipeline: the next filter, or the handler.</param>
    /// <returns>What <paramref name="next"/> returned, or a failure.</returns>
    ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next);
}
