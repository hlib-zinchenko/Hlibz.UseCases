namespace Hlibz.UseCases;

/// <summary>
/// The use cases and validatable types the package's source generator found in an assembly,
/// which <see cref="UseCaseServiceCollectionExtensions.AddUseCases"/> reads instead of scanning
/// the assembly. The generator derives one per assembly and applies it to the assembly; don't
/// derive or apply it yourself.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class UseCaseManifestAttribute : Attribute
{
    /// <summary>
    /// Gets the assembly's handlers, in declaration order.
    /// </summary>
    public abstract IReadOnlyList<UseCaseRegistration> UseCases { get; }

    /// <summary>
    /// Gets the types reachable from the assembly's use cases that have a rule to break.
    /// </summary>
    public abstract IReadOnlyList<ValidatableType> ValidatableTypes { get; }
}

/// <summary>
/// One handler in a <see cref="UseCaseManifestAttribute"/>: its use case, its result and how to
/// register it with its pipeline. Written by the source generator.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class UseCaseRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UseCaseRegistration"/> class.
    /// </summary>
    /// <param name="useCaseType">The use case type.</param>
    /// <param name="handlerType">The handler type.</param>
    /// <param name="resultType">
    /// The handler's <c>TResult</c>, or <see langword="null"/> when it returns no value.
    /// </param>
    /// <param name="register">Registers the handler and its pipeline.</param>
    public UseCaseRegistration(
        Type useCaseType,
        Type handlerType,
        Type? resultType,
        Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(useCaseType);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(register);

        UseCaseType = useCaseType;
        HandlerType = handlerType;
        ResultType = resultType;
        Register = register;
    }

    /// <summary>
    /// Gets the use case type.
    /// </summary>
    public Type UseCaseType { get; }

    /// <summary>
    /// Gets the handler type.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Gets the handler's <c>TResult</c>, or <see langword="null"/> when it returns no value.
    /// </summary>
    public Type? ResultType { get; }

    /// <summary>
    /// Gets the action that registers the handler and its pipeline.
    /// </summary>
    public Action<IServiceCollection> Register { get; }
}
