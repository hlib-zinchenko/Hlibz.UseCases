namespace Hlibz.UseCases;

/// <summary>
/// How use cases run, configured through
/// <see cref="UseCaseServiceCollectionExtensions.AddUseCases"/>.
/// </summary>
public sealed class UseCaseOptions
{
    /// <summary>
    /// Gets how use cases are validated before their handler runs.
    /// </summary>
    public UseCaseValidationOptions Validation { get; } = new();
}

/// <summary>
/// How use cases are validated before their handler runs.
/// </summary>
public sealed class UseCaseValidationOptions
{
    /// <summary>
    /// The default for <see cref="MaxDepth"/>, the same as ASP.NET Core's validation.
    /// </summary>
    public const int DefaultMaxDepth = 32;

    /// <summary>
    /// Gets or sets a value indicating whether a use case is validated before its handler runs.
    /// On by default. An invalid use case fails with Hlibz.Results' <see cref="ValidationFailed"/>,
    /// its errors keyed by property path (<c>Costs[0].Value</c>).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how deep nested objects are validated before validation gives up with an
    /// exception, which is almost always a circular reference. 32 by default.
    /// </summary>
    public int MaxDepth { get; set; } = DefaultMaxDepth;

    /// <summary>
    /// Gets or sets how a property without a <see cref="DisplayAttribute"/> name is named in
    /// messages, e.g. <see cref="JsonNamingPolicy.SnakeCaseLower"/> so that
    /// <c>The first_name field is required.</c> names the field as clients send it. The
    /// property's own name when <see langword="null"/> (the default), as
    /// Microsoft.Extensions.Validation does. Error keys aren't affected: they stay property
    /// paths (<c>Costs[0].Value</c>), for the transport to rename.
    /// </summary>
    public JsonNamingPolicy? DisplayNamePolicy { get; set; }
}
