namespace Hlibz.UseCases.Internal;

/// <summary>
/// <see cref="IUseCaseValidator"/> for one scope.
/// </summary>
internal sealed class UseCaseValidator : IUseCaseValidator
{
    private readonly ValidationEngine _engine;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseCaseValidator"/> class.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="services">The scope's services.</param>
    public UseCaseValidator(ValidationEngine engine, IServiceProvider services)
    {
        _engine = engine;
        _services = services;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyDictionary<string, string[]>?> ValidateAsync(
        object value,
        CancellationToken cancellationToken = default) =>
        _engine.ValidateAsync(value, _services, cancellationToken);
}
