namespace Atlas.Application.Cities;

/// <summary>
/// A two-letter ISO country code in capitals. Describes itself (<see cref="IDescribedRule"/>), so
/// an OpenAPI document or a tool schema can say what it checks (Hlibz.OpenApi.UseCases does).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class CountryCodeAttribute : ValidationAttribute, IDescribedRule
{
    public const string Text = "A two-letter ISO 3166 country code in capitals, such as UA.";

    public CountryCodeAttribute()
        : base("The {0} field must be a two-letter country code in capitals.")
    {
    }

    public override bool IsValid(object? value) =>
        value is null || (value is string { Length: 2 } code && code.All(char.IsAsciiLetterUpper));

    public void Describe(RuleDescription rule)
    {
        rule.Pattern = "^[A-Z]{2}$";
        rule.AddText(Text);
    }
}
