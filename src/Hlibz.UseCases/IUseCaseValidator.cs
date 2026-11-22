namespace Hlibz.UseCases;

/// <summary>
/// Validates a use case, or any type reachable from one, the way its pipeline does, without
/// handling it: for a client that wants to check input early. Scoped, so validation attributes
/// can resolve the scope's services through their <see cref="ValidationContext"/>.
/// </summary>
public interface IUseCaseValidator
{
    /// <summary>
    /// Validates <paramref name="value"/> against its DataAnnotations rules.
    /// </summary>
    /// <param name="value">The use case (or a type reachable from one).</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>
    /// The errors keyed by property path (<c>Costs[0].Value</c>; <c>""</c> for a rule on the
    /// whole object), or <see langword="null"/> when there are none or the type has no rule.
    /// </returns>
    ValueTask<IReadOnlyDictionary<string, string[]>?> ValidateAsync(
        object value,
        CancellationToken cancellationToken = default);
}
