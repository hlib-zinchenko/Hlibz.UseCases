using System.Text.Json.Nodes;

namespace Hlibz.UseCases;

/// <summary>
/// What an <see cref="IDescribedRule"/> checks, shaped like JSON Schema so an adapter can copy it
/// into an OpenAPI schema or a tool's input schema: the keywords JSON Schema has for it, and
/// sentences for people.
/// </summary>
public sealed class RuleDescription
{
    private readonly List<string> _texts = [];

    /// <summary>
    /// Gets the sentences describing the rule, in the order they were added.
    /// </summary>
    public IReadOnlyList<string> Texts => _texts;

    /// <summary>
    /// Gets or sets the regular expression a string must match (<c>pattern</c>).
    /// </summary>
    [StringSyntax(StringSyntaxAttribute.Regex)]
    public string? Pattern { get; set; }

    /// <summary>
    /// Gets or sets the shortest a string may be (<c>minLength</c>).
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Gets or sets the longest a string may be (<c>maxLength</c>).
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the smallest number allowed (<c>minimum</c>).
    /// </summary>
    public decimal? Minimum { get; set; }

    /// <summary>
    /// Gets or sets the largest number allowed (<c>maximum</c>).
    /// </summary>
    public decimal? Maximum { get; set; }

    /// <summary>
    /// Gets or sets the number a value must be greater than (<c>exclusiveMinimum</c>).
    /// </summary>
    public decimal? ExclusiveMinimum { get; set; }

    /// <summary>
    /// Gets or sets the number a value must be less than (<c>exclusiveMaximum</c>).
    /// </summary>
    public decimal? ExclusiveMaximum { get; set; }

    /// <summary>
    /// Gets or sets the fewest items a collection may have (<c>minItems</c>).
    /// </summary>
    public int? MinItems { get; set; }

    /// <summary>
    /// Gets or sets the most items a collection may have (<c>maxItems</c>).
    /// </summary>
    public int? MaxItems { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a collection's items must be distinct
    /// (<c>uniqueItems</c>).
    /// </summary>
    public bool? UniqueItems { get; set; }

    /// <summary>
    /// Gets or sets a value the field must not be, as JSON (<c>not: { const: ... }</c>), such as an
    /// empty GUID.
    /// </summary>
    public JsonNode? NotConst { get; set; }

    /// <summary>
    /// Gets any other JSON Schema keywords, copied into the schema as they are.
    /// </summary>
    public JsonObject Keywords { get; } = [];

    /// <summary>
    /// Adds a sentence describing the rule. Adding the same sentence twice does nothing.
    /// </summary>
    /// <param name="text">The rule, as a sentence.</param>
    public void AddText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!_texts.Contains(text, StringComparer.Ordinal))
        {
            _texts.Add(text);
        }
    }
}
