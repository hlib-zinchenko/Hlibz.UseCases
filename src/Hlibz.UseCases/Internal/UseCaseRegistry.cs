namespace Hlibz.UseCases.Internal;

/// <summary>
/// Everything the generated manifests of the registered assemblies hold: the use cases, and the
/// validatable types by type. One per service collection, filled while services are registered
/// and read-only once the container is built.
/// </summary>
internal sealed class UseCaseRegistry : IUseCaseCatalog
{
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly List<UseCaseRegistration> _registrations = [];
    private readonly Dictionary<Type, ValidatableType> _validatableTypes = [];
    private readonly Dictionary<Type, UseCaseDescriptor> _byUseCase = [];
    private List<UseCaseDescriptor>? _useCases;

    /// <inheritdoc/>
    public IReadOnlyList<UseCaseDescriptor> UseCases =>
        _useCases ??=
        [
            .. _registrations.Select(registration => _byUseCase[registration.UseCaseType]),
        ];

    /// <summary>
    /// Adds an assembly's manifest, once.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>
    /// The handlers to register, or none when the assembly was already added.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The assembly wasn't compiled with the source generator, or a use case it handles already
    /// has a handler in another assembly.
    /// </exception>
    public IReadOnlyList<UseCaseRegistration> Add(Assembly assembly)
    {
        if (!_assemblies.Add(assembly))
        {
            return [];
        }

        UseCaseManifestAttribute manifest = assembly.GetCustomAttribute<UseCaseManifestAttribute>()
            ?? throw new InvalidOperationException(
                $"{assembly.GetName().Name} has no use case list: the Hlibz.UseCases source "
                + "generator didn't run when it was compiled. Reference the Hlibz.UseCases "
                + "package from that project, and don't exclude its analyzers.");

        foreach (ValidatableType type in manifest.ValidatableTypes)
        {
            // A type reachable from use cases in two assemblies is listed by both, the same way.
            _validatableTypes.TryAdd(type.Type, type);
        }

        foreach (UseCaseRegistration registration in manifest.UseCases)
        {
            if (_byUseCase.TryGetValue(registration.UseCaseType, out UseCaseDescriptor? existing))
            {
                throw new InvalidOperationException(
                    $"{registration.UseCaseType} has two handlers: {existing.HandlerType} and "
                    + $"{registration.HandlerType}. A use case has exactly one handler.");
            }

            _byUseCase.Add(
                registration.UseCaseType,
                new UseCaseDescriptor(
                    registration.UseCaseType,
                    registration.HandlerType,
                    registration.ResultType,
                    _validatableTypes.ContainsKey(registration.UseCaseType)));
            _registrations.Add(registration);
        }

        _useCases = null;
        return manifest.UseCases;
    }

    /// <inheritdoc/>
    public UseCaseDescriptor? Find(Type useCaseType)
    {
        ArgumentNullException.ThrowIfNull(useCaseType);
        return _byUseCase.GetValueOrDefault(useCaseType);
    }

    /// <summary>
    /// Finds the validatable type for a value's runtime type.
    /// </summary>
    /// <param name="type">The runtime type.</param>
    /// <param name="validatable">The validatable type, when there is one.</param>
    /// <returns>Whether the type has a rule to break.</returns>
    public bool TryGetValidatableType(
        Type type,
        [NotNullWhen(true)] out ValidatableType? validatable) =>
        _validatableTypes.TryGetValue(type, out validatable);
}
