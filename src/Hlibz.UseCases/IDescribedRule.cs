namespace Hlibz.UseCases;

/// <summary>
/// A validation attribute that says what it checks, for adapters that publish rules to the outside
/// world: an OpenAPI document (Hlibz.OpenApi.UseCases), MCP tool input schemas.
/// </summary>
/// <remarks>
/// DataAnnotations' own attributes (<c>[Required]</c>, <c>[Range]</c>, <c>[MaxLength]</c>, ...)
/// need nothing: those adapters already know them. Implement this on a custom
/// <see cref="ValidationAttribute"/>, and set a JSON Schema keyword wherever there is one, so
/// clients generated from a schema can enforce the rule, plus a sentence for people. Each
/// attribute is read once and shared, so <see cref="Describe"/> writes to the description only,
/// never to the attribute itself.
/// </remarks>
/// <example>
/// <code>
/// public sealed class StrongPasswordAttribute : ValidationAttribute, IDescribedRule
/// {
///     public override bool IsValid(object? value) => ...;
///
///     public void Describe(RuleDescription rule)
///     {
///         rule.Pattern = "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).*$";
///         rule.AddText("Must contain a lowercase and an uppercase letter and a digit.");
///     }
/// }
/// </code>
/// </example>
public interface IDescribedRule
{
    /// <summary>
    /// Writes this rule into <paramref name="rule"/>.
    /// </summary>
    /// <param name="rule">The description of the property, parameter or type it sits on.</param>
    void Describe(RuleDescription rule);
}
