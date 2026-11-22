using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hlibz.UseCases.Generators;

/// <summary>
/// Lists every concrete use case handler of the assembly being compiled in a generated
/// <c>UseCaseManifestAttribute</c> applied to the assembly, which <c>AddUseCases</c> reads instead
/// of scanning types at runtime, with a pipeline class per handler and the validation tables of
/// the types its use cases reach. A handler it can't register is a build error.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class UseCaseManifestGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<HandlerModel> handlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node
                    is ClassDeclarationSyntax { BaseList: not null }
                    or StructDeclarationSyntax { BaseList: not null }
                    or RecordDeclarationSyntax { BaseList: not null },
                static (syntaxContext, cancellationToken) =>
                    HandlerModelBuilder.Build(syntaxContext, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        IncrementalValueProvider<string?> manifestClassName = context.CompilationProvider
            .Select(static (compilation, _) => ManifestWriter.ClassNameFor(compilation));

        context.RegisterSourceOutput(
            handlers.Collect().Combine(manifestClassName),
            static (sourceContext, input) => Emit(sourceContext, input.Left, input.Right));
    }

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<HandlerModel> handlers,
        string? manifestClassName)
    {
        // A partial handler declared with its base list in more than one file is found once per
        // declaration; keep the first.
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<HandlerModel> distinct = [.. handlers.Where(handler => seen.Add(handler.HandlerName))];

        foreach (DiagnosticInfo diagnostic in distinct.SelectMany(handler => handler.Diagnostics))
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        List<HandlerModel> usable = [.. distinct.Where(handler => handler.Diagnostics.Count == 0)];
        HashSet<string> duplicated = new(StringComparer.Ordinal);
        foreach (IGrouping<string, HandlerModel> group in usable
                     .GroupBy(handler => handler.UseCaseName, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            duplicated.Add(group.Key);
            HandlerModel[] all = [.. group];
            foreach (HandlerModel handler in all)
            {
                HandlerModel other = all.First(item => !ReferenceEquals(item, handler));
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ManyHandlers,
                    Location.Create(handler.Location.FilePath, handler.Location.TextSpan, handler.Location.LineSpan),
                    Display(handler.UseCaseName),
                    Display(handler.HandlerName),
                    Display(other.HandlerName)));
            }
        }

        if (manifestClassName is null)
        {
            return;
        }

        List<HandlerModel> registered = [.. usable.Where(handler => !duplicated.Contains(handler.UseCaseName))];
        HashSet<string> typeNames = new(StringComparer.Ordinal);
        List<TypeModel> types =
        [
            .. registered
                .SelectMany(handler => handler.Types)
                .Where(type => type.CanFail && typeNames.Add(type.Name)),
        ];

        context.AddSource("UseCaseManifest.g.cs", ManifestWriter.Write(manifestClassName, registered, types));
    }

    private static string Display(string name) => name.Replace("global::", string.Empty);
}
