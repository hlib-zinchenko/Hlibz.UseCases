using System.Collections.Concurrent;

namespace Hlibz.UseCases.Internal;

/// <summary>
/// The validation attributes and display names of properties and types, read once each.
/// <c>GetCustomAttributes</c> creates new attribute instances on every call, and validation runs
/// for every use case handled.
/// </summary>
/// <remarks>
/// A property's attributes are its own (inherited ones included) plus those on the matching
/// parameter of the first public constructor that has one (case-insensitive), which is where a
/// positional record's attributes land: the same lookup as Microsoft.Extensions.Validation.
/// </remarks>
internal static class RuleAttributes
{
    private const BindingFlags DeclaredInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyRules>
        Properties = new();

    private static readonly ConcurrentDictionary<Type, ValidationAttribute[]> Types = new();

    /// <summary>
    /// Gets a property's validation attributes and display name.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>Its rules.</returns>
    public static PropertyRules For(ValidatableProperty property) =>
        Properties.GetOrAdd(
            (property.DeclaringType, property.Name),
            static (_, value) => Read(value.DeclaringType, value.Name),
            property);

    /// <summary>
    /// Gets a type's own validation attributes (inherited ones included).
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The attributes; empty when there are none.</returns>
    public static ValidationAttribute[] For(Type type) =>
        Types.GetOrAdd(type, static key => [.. key.GetCustomAttributes<ValidationAttribute>(inherit: true)]);

    private static PropertyRules Read(
        [DynamicallyAccessedMembers(ValidatableProperty.Members)] Type declaringType,
        string name)
    {
        List<ValidationAttribute> attributes = [];
        DisplayAttribute? display = null;

        PropertyInfo? property = declaringType.GetProperty(name, DeclaredInstance);
        if (property is not null)
        {
            attributes.AddRange(property.GetCustomAttributes<ValidationAttribute>(inherit: true));
            display = property.GetCustomAttribute<DisplayAttribute>(inherit: true);
        }

        ParameterInfo? parameter = declaringType.GetConstructors()
            .Select(constructor => constructor.GetParameters().FirstOrDefault(
                candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(candidate => candidate is not null);
        if (parameter is not null)
        {
            attributes.AddRange(parameter.GetCustomAttributes<ValidationAttribute>(inherit: true));
            display ??= parameter.GetCustomAttribute<DisplayAttribute>(inherit: true);
        }

        return new PropertyRules(
            [.. attributes],
            attributes.OfType<RequiredAttribute>().FirstOrDefault(),
            display);
    }
}

/// <summary>
/// A property's validation attributes and display name.
/// </summary>
/// <param name="Attributes">Every validation attribute, <see cref="Required"/> included.</param>
/// <param name="Required">The <see cref="RequiredAttribute"/>, checked first, if any.</param>
/// <param name="Display">The <see cref="DisplayAttribute"/> naming it in messages, if any.</param>
internal sealed record PropertyRules(
    ValidationAttribute[] Attributes,
    RequiredAttribute? Required,
    DisplayAttribute? Display);
