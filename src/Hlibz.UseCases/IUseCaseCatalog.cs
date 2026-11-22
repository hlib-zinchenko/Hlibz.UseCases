namespace Hlibz.UseCases;

/// <summary>
/// Every use case registered with
/// <see cref="UseCaseServiceCollectionExtensions.AddUseCases"/>, for adapters that describe them
/// to the outside world (an OpenAPI document, MCP tool schemas). A singleton.
/// </summary>
public interface IUseCaseCatalog
{
    /// <summary>
    /// Gets every registered use case, in registration order.
    /// </summary>
    IReadOnlyList<UseCaseDescriptor> UseCases { get; }

    /// <summary>
    /// Finds a use case by its type.
    /// </summary>
    /// <param name="useCaseType">The use case type.</param>
    /// <returns>The use case, or <see langword="null"/> when none is registered.</returns>
    UseCaseDescriptor? Find(Type useCaseType);
}
