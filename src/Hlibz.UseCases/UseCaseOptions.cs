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
    /// On by default.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the status of the failure an invalid use case returns:
    /// <see cref="ResultStatus.Invalid"/> (the default) or <see cref="ResultStatus.Error"/>, for
    /// apps that don't tell validation apart from other bad requests. Either way the failure
    /// carries <see cref="Result.ValidationErrors"/>, keyed by property path
    /// (<c>Costs[0].Value</c>).
    /// </summary>
    public ResultStatus FailureStatus { get; set; } = ResultStatus.Invalid;

    /// <summary>
    /// Gets or sets the failure's message; Hlibz.Results' default validation message when
    /// <see langword="null"/>.
    /// </summary>
    public string? FailureMessage { get; set; }

    /// <summary>
    /// Gets or sets how deep nested objects are validated before validation gives up with an
    /// exception, which is almost always a circular reference. 32 by default.
    /// </summary>
    public int MaxDepth { get; set; } = DefaultMaxDepth;
}
