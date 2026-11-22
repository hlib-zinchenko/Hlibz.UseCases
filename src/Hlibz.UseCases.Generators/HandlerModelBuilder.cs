using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hlibz.UseCases.Generators;

/// <summary>
/// Turns a type declaration into a <see cref="HandlerModel"/> when it is a handler, with the
/// reasons it can't be registered when it is an unusable one.
/// </summary>
internal static class HandlerModelBuilder
{
    /// <summary>
    /// Describes the class or struct declared at <paramref name="context"/>'s node.
    /// </summary>
    /// <param name="context">The type declaration and its semantic model.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>
    /// The handler, or <see langword="null"/> when the type isn't one, is abstract (a base for
    /// handlers), or doesn't compile yet.
    /// </returns>
    public static HandlerModel? Build(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        Compilation compilation = context.SemanticModel.Compilation;
        if (context.Node is not TypeDeclarationSyntax declaration
            || context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken)
                is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } type
            || type.IsAbstract
            || compilation.GetTypeByMetadataName(SymbolNames.HandlerMarker) is not { } marker
            || !type.AllInterfaces.Contains(marker, SymbolEqualityComparer.Default))
        {
            return null;
        }

        Location location = declaration.Identifier.GetLocation();
        string name = type.ToDisplayString();
        LocationInfo locationInfo = new(
            location.SourceTree?.FilePath ?? string.Empty,
            location.SourceSpan,
            location.GetLineSpan().Span);

        if (type.TypeKind == TypeKind.Struct)
        {
            return Unregistered(type, locationInfo, DiagnosticInfo.Create(Diagnostics.StructHandler, location, name));
        }

        if (IsGeneric(type))
        {
            return Unregistered(type, locationInfo, DiagnosticInfo.Create(Diagnostics.GenericHandler, location, name));
        }

        INamedTypeSymbol[] handled = HandledInterfaces(type, compilation);
        if (handled.Any(item => item.TypeArguments.Any(argument => argument.TypeKind == TypeKind.Error)))
        {
            // The compiler reports that one; a second error for it would only be noise.
            return null;
        }

        if (handled.Length != 1)
        {
            string implemented = handled.Length == 0
                ? "none"
                : string.Join(", ", handled.Select(item => item.ToDisplayString()));
            return Unregistered(
                type,
                locationInfo,
                DiagnosticInfo.Create(Diagnostics.NotOneUseCase, location, name, implemented));
        }

        INamedTypeSymbol handler = handled[0];
        ITypeSymbol useCase = handler.TypeArguments[0];
        ITypeSymbol? result = handler.TypeArguments.Length == 2 ? handler.TypeArguments[1] : null;
        INamedTypeSymbol[] own =
        [
            .. type.AllInterfaces.Where(candidate =>
                !SymbolEqualityComparer.Default.Equals(candidate, handler)
                && !SymbolEqualityComparer.Default.Equals(candidate, marker)
                && candidate.AllInterfaces.Contains(handler, SymbolEqualityComparer.Default)),
        ];

        List<DiagnosticInfo> diagnostics = [];
        IEnumerable<ITypeSymbol> named = new ITypeSymbol[] { type, useCase }
            .Concat(result is null ? [] : [result])
            .Concat(own);
        foreach (ITypeSymbol item in named.Where(item => !compilation.IsSymbolAccessibleWithin(item, compilation.Assembly)))
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.Inaccessible, location, item.ToDisplayString()));
        }

        foreach (INamedTypeSymbol item in own.Where(item => item.GetMembers().Length > 0))
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.InterfaceWithMembers, location, item.ToDisplayString()));
        }

        if (!type.InstanceConstructors.Any(constructor => constructor.DeclaredAccessibility == Accessibility.Public))
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NoPublicConstructor, location, name));
        }

        EquatableArray<TypeModel> types = diagnostics.Count == 0
            ? ValidationGraph.Build(useCase, compilation, cancellationToken)
            : new EquatableArray<TypeModel>([]);
        return new HandlerModel(
            type.ToDisplayString(SymbolNames.TypeOf),
            useCase.ToDisplayString(SymbolNames.TypeOf),
            useCase.ToDisplayString(SymbolNames.Argument),
            result?.ToDisplayString(SymbolNames.TypeOf),
            result?.ToDisplayString(SymbolNames.Argument),
            new EquatableArray<string>([.. own.Select(item => item.ToDisplayString(SymbolNames.Argument))]),
            locationInfo,
            new EquatableArray<DiagnosticInfo>([.. diagnostics]),
            types);
    }

    /// <summary>
    /// A handler that can't be registered for what it is, so nothing else about it is checked.
    /// </summary>
    private static HandlerModel Unregistered(
        INamedTypeSymbol type,
        LocationInfo location,
        DiagnosticInfo diagnostic) =>
        new(
            type.ToDisplayString(SymbolNames.TypeOf),
            string.Empty,
            string.Empty,
            ResultName: null,
            ResultArgument: null,
            new EquatableArray<string>([]),
            location,
            new EquatableArray<DiagnosticInfo>([diagnostic]),
            new EquatableArray<TypeModel>([]));

    /// <summary>
    /// The distinct closed <c>IUseCaseHandler&lt;...&gt;</c> interfaces the type implements.
    /// </summary>
    private static INamedTypeSymbol[] HandledInterfaces(INamedTypeSymbol type, Compilation compilation)
    {
        INamedTypeSymbol? plain = compilation.GetTypeByMetadataName(SymbolNames.Handler);
        INamedTypeSymbol? withResult = compilation.GetTypeByMetadataName(SymbolNames.HandlerWithResult);
        return
        [
            .. type.AllInterfaces.Where(item =>
                SymbolEqualityComparer.Default.Equals(item.OriginalDefinition, plain)
                || SymbolEqualityComparer.Default.Equals(item.OriginalDefinition, withResult)),
        ];
    }

    /// <summary>
    /// Whether the type or a type it is nested in has type parameters.
    /// </summary>
    private static bool IsGeneric(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeParameters.Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
