using Microsoft.CodeAnalysis.Text;

namespace Hlibz.UseCases.Generators;

/// <summary>
/// One handler as the generator sees it: plain strings, so the incremental pipeline can tell
/// when nothing changed.
/// </summary>
/// <param name="HandlerName">The handler type, fully qualified with <c>global::</c>.</param>
/// <param name="UseCaseName">The use case type, as in <c>typeof</c>.</param>
/// <param name="UseCaseArgument">The use case type as a type argument (nullable annotations kept).</param>
/// <param name="ResultName">The handler's <c>TResult</c> as in <c>typeof</c>, or null.</param>
/// <param name="ResultArgument">The handler's <c>TResult</c> as a type argument, or null.</param>
/// <param name="ErrorName">The handler's <c>TError</c> as in <c>typeof</c>, or null.</param>
/// <param name="ErrorArgument">The handler's <c>TError</c> as a type argument, or null.</param>
/// <param name="Interfaces">The handler's own interfaces, as type arguments.</param>
/// <param name="Location">Where the handler is declared.</param>
/// <param name="Diagnostics">Why it can't be registered; empty when it can.</param>
/// <param name="Types">The types its use case reaches, for validation.</param>
internal sealed record HandlerModel(
    string HandlerName,
    string UseCaseName,
    string UseCaseArgument,
    string? ResultName,
    string? ResultArgument,
    string? ErrorName,
    string? ErrorArgument,
    EquatableArray<string> Interfaces,
    LocationInfo Location,
    EquatableArray<DiagnosticInfo> Diagnostics,
    EquatableArray<TypeModel> Types)
{
    /// <summary>
    /// Gets the closed <c>IUseCaseHandler&lt;...&gt;</c> the handler implements.
    /// </summary>
    public string HandlerInterface => $"global::Hlibz.UseCases.IUseCaseHandler<{TypeArguments}>";

    /// <summary>
    /// Gets the handler's result type: a <c>Result</c>, <c>Result&lt;TResult&gt;</c> or
    /// <c>Result&lt;TResult, TError&gt;</c>.
    /// </summary>
    public string ResultType => ResultArgument is null
        ? "global::Hlibz.Results.Result"
        : $"global::Hlibz.Results.Result<{string.Join(", ", TypeArgumentList.Skip(1))}>";

    /// <summary>
    /// Gets the closed interface's type arguments, comma separated.
    /// </summary>
    public string TypeArguments => string.Join(", ", TypeArgumentList);

    private IEnumerable<string> TypeArgumentList =>
        new[] { UseCaseArgument, ResultArgument, ErrorArgument }.OfType<string>();
}

/// <summary>
/// A type a use case reaches, and whether validating it can fail.
/// </summary>
/// <param name="Name">The type, as in <c>typeof</c>.</param>
/// <param name="CanFail">Whether it has a rule to break, itself or inside.</param>
/// <param name="Properties">The properties to validate, in order.</param>
internal sealed record TypeModel(
    string Name,
    bool CanFail,
    EquatableArray<PropertyModel> Properties);

/// <summary>
/// A property of a <see cref="TypeModel"/> to validate.
/// </summary>
/// <param name="Name">The property's name, escaped where it is a keyword.</param>
/// <param name="MetadataName">The property's name as reflection sees it.</param>
/// <param name="DeclaringTypeName">The type declaring it, as in <c>typeof</c>.</param>
/// <param name="IsCollection">Whether its items are validated one by one.</param>
internal sealed record PropertyModel(
    string Name,
    string MetadataName,
    string DeclaringTypeName,
    bool IsCollection);

/// <summary>
/// A place in a source file, kept as values so the incremental pipeline can cache it.
/// </summary>
/// <param name="FilePath">The file.</param>
/// <param name="TextSpan">Where in the file.</param>
/// <param name="LineSpan">The same place as lines and columns.</param>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan);
