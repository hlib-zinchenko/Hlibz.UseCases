using Microsoft.Extensions.Validation;

namespace Hlibz.UseCases.Tests;

// The models both engines validate: marked [ValidatableType] for Microsoft.Extensions.Validation's
// generator (in this project because ParityTests calls AddValidation()), and reachable from
// ParityRoot, a use case, for this package's generator.
[ValidatableType]
public sealed record ParityRoot(
    RequiredFirst RequiredFirst,
    ManyRules ManyRules,
    Order Order,
    WholeObjectRules WholeObjectRules,
    TypeRuleFirst TypeRuleFirst,
    Displayed Displayed,
    Derived Derived,
    Passwords Passwords,
    Deep Deep,
    WithStruct WithStruct) : IUseCase;

internal sealed class ParityRootHandler : IUseCaseHandler<ParityRoot>
{
    public Task<Result> HandleAsync(ParityRoot useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}

/// <summary>Required fails first and alone; the other attributes run only once it passes.</summary>
[ValidatableType]
public sealed record RequiredFirst([Required, MinLength(3)] string? Name);

/// <summary>Every failing attribute of a property is reported, in order.</summary>
[ValidatableType]
public sealed record ManyRules([MinLength(5), RegularExpression("^[a-z]+$"), MaxLength(1)] string Code);

/// <summary>Nested objects, lists, arrays and null items, by path.</summary>
[ValidatableType]
public sealed record Order(
    [Range(1, 10)] int Priority,
    Line? Main,
    IList<Line?> Lines,
#pragma warning disable CA1819 // An array on purpose: arrays are validated item by item too.
    Line[] Extra,
#pragma warning restore CA1819
    IReadOnlyDictionary<string, Line> ByName);

[ValidatableType]
public sealed record Line([Required] string? Sku, [Range(1, 100)] int Quantity);

/// <summary>
/// IValidatableObject runs only when every property passed; a result naming no member is keyed
/// "" wherever the object sits.
/// </summary>
[ValidatableType]
public sealed record WholeObjectRules([Range(0, 10)] int Min, [Range(0, 10)] int Max, Window Window)
    : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Min > Max)
        {
            yield return new ValidationResult("Min must not exceed Max.", [nameof(Min), nameof(Max)]);
        }

        if (Min == 7)
        {
            yield return new ValidationResult("Seven is unlucky.");
        }
    }
}

[ValidatableType]
public sealed record Window(int From, int To) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From > To)
        {
            yield return new ValidationResult("The window is backwards.");
            yield return new ValidationResult("From is after To.", [nameof(From)]);
        }
    }
}

/// <summary>A type-level attribute runs after the properties, and stops IValidatableObject.</summary>
[ValidatableType]
[NotBothEmpty]
public sealed record TypeRuleFirst(string? First, string? Second, Window Window) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield return new ValidationResult("Only when the type rule passed.");
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class NotBothEmptyAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is TypeRuleFirst { First: null or "", Second: null or "" })
        {
            return new ValidationResult("Give First or Second.", [nameof(TypeRuleFirst.First)]);
        }

        return value is TypeRuleFirst { First: "both" }
            ? new ValidationResult("Not both.")
            : ValidationResult.Success;
    }
}

/// <summary>[Display] names the field in messages, on a property or a record parameter.</summary>
[ValidatableType]
public sealed class Displayed
{
    [Display(Name = "Full name")]
    [Required]
    public string? Name { get; init; }

    [Range(18, 120, ErrorMessage = "{0} must be between {1} and {2}.")]
    public int Age { get; init; }
}

/// <summary>Inherited properties are validated with the derived type.</summary>
[ValidatableType]
public record BaseModel([Required] string? Id);

[ValidatableType]
public sealed record Derived(string? Id, [Range(1, 5)] int Level) : BaseModel(Id);

/// <summary>An attribute that reads another property through the ValidationContext.</summary>
[ValidatableType]
public sealed class Passwords
{
    [Required]
    public string? Password { get; init; }

    [Compare(nameof(Password))]
    public string? Confirm { get; init; }
}

/// <summary>Paths several levels down.</summary>
[ValidatableType]
public sealed record Deep(IReadOnlyList<Level1> Levels);

[ValidatableType]
public sealed record Level1(Level2 Inner);

[ValidatableType]
public sealed record Level2(IReadOnlyList<Line> Lines);

/// <summary>A nested struct.</summary>
[ValidatableType]
public sealed record WithStruct(Money Price, Money? Discount);

public readonly record struct Money([Range(0, 1000)] decimal Amount, [Required] string? Currency);
