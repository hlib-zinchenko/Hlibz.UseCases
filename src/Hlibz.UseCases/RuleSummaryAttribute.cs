namespace Hlibz.UseCases;

/// <summary>
/// Summarizes, in a sentence, a rule that has no attribute of its own: a cross-field check in
/// <see cref="IValidatableObject.Validate"/>, or one the handler enforces. It checks nothing; it
/// only tells adapters (an OpenAPI document, MCP tool schemas) what to publish.
/// </summary>
/// <example>
/// <code>
/// [RuleSummary("End must not be earlier than Start.")]
/// public sealed record PlanTripCommand(DateOnly Start, DateOnly End) : IUseCase, IValidatableObject { ... }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property
    | AttributeTargets.Field | AttributeTargets.Parameter,
    AllowMultiple = true)]
public sealed class RuleSummaryAttribute : Attribute, IDescribedRule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuleSummaryAttribute"/> class.
    /// </summary>
    /// <param name="summary">The rule, as a sentence.</param>
    public RuleSummaryAttribute(string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        Summary = summary;
    }

    /// <summary>
    /// Gets the rule, as a sentence.
    /// </summary>
    public string Summary { get; }

    /// <inheritdoc/>
    public void Describe(RuleDescription rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        rule.AddText(Summary);
    }
}
