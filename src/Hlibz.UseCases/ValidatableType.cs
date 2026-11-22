namespace Hlibz.UseCases;

/// <summary>
/// A type with a rule to break, as the source generator found it: the members to validate and
/// how to read them without reflection. Written by the source generator.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValidatableType
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatableType"/> class.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="properties">
    /// Its properties that have a rule of their own or hold something validatable, inherited
    /// ones included, in declaration order.
    /// </param>
    public ValidatableType(Type type, IReadOnlyList<ValidatableProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(properties);

        Type = type;
        Properties = properties;
    }

    /// <summary>
    /// Gets the type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the properties to validate.
    /// </summary>
    public IReadOnlyList<ValidatableProperty> Properties { get; }
}

/// <summary>
/// A property of a <see cref="ValidatableType"/>. Written by the source generator.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ValidatableProperty
{
    /// <summary>
    /// What the validation reads from the declaring type: its properties' and its constructors'
    /// parameters' attributes.
    /// </summary>
    internal const DynamicallyAccessedMemberTypes Members =
        DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicConstructors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatableProperty"/> class.
    /// </summary>
    /// <param name="declaringType">
    /// The type declaring the property, whose property and constructor parameters carry its
    /// validation attributes.
    /// </param>
    /// <param name="name">The property's name.</param>
    /// <param name="getValue">Reads the property from an instance.</param>
    /// <param name="isCollection">
    /// Whether the property's type is a collection (not a <see cref="string"/>), whose items are
    /// validated one by one.
    /// </param>
    public ValidatableProperty(
        [DynamicallyAccessedMembers(Members)] Type declaringType,
        string name,
        Func<object, object?> getValue,
        bool isCollection)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(getValue);

        DeclaringType = declaringType;
        Name = name;
        GetValue = getValue;
        IsCollection = isCollection;
    }

    /// <summary>
    /// Gets the type declaring the property.
    /// </summary>
    [DynamicallyAccessedMembers(Members)]
    public Type DeclaringType { get; }

    /// <summary>
    /// Gets the property's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the function that reads the property from an instance.
    /// </summary>
    public Func<object, object?> GetValue { get; }

    /// <summary>
    /// Gets a value indicating whether the property's items are validated one by one.
    /// </summary>
    public bool IsCollection { get; }
}
