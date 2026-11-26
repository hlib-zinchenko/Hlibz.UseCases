using Microsoft.CodeAnalysis;

namespace Hlibz.UseCases.Generators;

/// <summary>
/// How generated code names types.
/// </summary>
internal static class SymbolNames
{
    /// <summary>
    /// The metadata name of the marker every handler implements.
    /// </summary>
    public const string HandlerMarker = "Hlibz.UseCases.IUseCaseHandler";

    /// <summary>
    /// The metadata name of the handler of a use case returning no value.
    /// </summary>
    public const string Handler = "Hlibz.UseCases.IUseCaseHandler`1";

    /// <summary>
    /// The metadata name of the handler of a use case returning a value.
    /// </summary>
    public const string HandlerWithResult = "Hlibz.UseCases.IUseCaseHandler`2";

    /// <summary>
    /// The metadata name of the handler of a use case declaring its errors.
    /// </summary>
    public const string HandlerWithErrors = "Hlibz.UseCases.IUseCaseHandler`3";

    /// <summary>
    /// The metadata name of the attribute the manifest derives from.
    /// </summary>
    public const string Manifest = "Hlibz.UseCases.UseCaseManifestAttribute";

    /// <summary>
    /// Fully qualified, as <c>typeof</c> accepts it: <c>global::</c>, keywords for special types,
    /// tuples as <c>ValueTuple</c>, no nullable annotations.
    /// </summary>
    public static readonly SymbolDisplayFormat TypeOf =
        SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.ExpandValueTuple)
            .RemoveMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Fully qualified as a type argument: nullable annotations and tuple element names kept, so
    /// the generated pipeline matches the interface it implements exactly.
    /// </summary>
    public static readonly SymbolDisplayFormat Argument =
        SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
}
