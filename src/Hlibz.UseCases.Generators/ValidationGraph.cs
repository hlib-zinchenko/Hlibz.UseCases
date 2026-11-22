using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hlibz.UseCases.Generators;

/// <summary>
/// Finds the types a use case reaches through its properties (and collection items), and which
/// of them have a rule to break: the same reach Microsoft.Extensions.Validation's generator has
/// from a <c>[ValidatableType]</c> root.
/// </summary>
/// <remarks>
/// A type has a rule when it has a validation attribute (inherited ones included), implements
/// <c>IValidatableObject</c> or <c>IAsyncValidatableObject</c>, has a property with a validation
/// attribute (on the property or on the matching parameter of a public constructor, as positional
/// records put them), or has a property whose type, or item type, has a rule. Only public instance
/// properties count, inherited ones included. Types in <c>System</c> namespaces, enums, interfaces
/// and types the generated code can't name are never walked.
/// </remarks>
internal static class ValidationGraph
{
    private const string ValidationAttributeName =
        "System.ComponentModel.DataAnnotations.ValidationAttribute";

    private static readonly string[] ValidatableObjectNames =
    [
        "System.ComponentModel.DataAnnotations.IValidatableObject",
        "System.ComponentModel.DataAnnotations.IAsyncValidatableObject",
    ];

    /// <summary>
    /// Walks the types <paramref name="root"/> reaches.
    /// </summary>
    /// <param name="root">The use case type.</param>
    /// <param name="compilation">The compilation.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Every type reached, the root first, each saying whether it can fail.</returns>
    public static EquatableArray<TypeModel> Build(
        ITypeSymbol root,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        Walker walker = new(compilation, cancellationToken);
        walker.Visit(root);
        return walker.Result();
    }

    private sealed class Node(INamedTypeSymbol type, bool hasOwnRule)
    {
        public INamedTypeSymbol Type { get; } = type;

        public bool HasOwnRule { get; } = hasOwnRule;

        public List<Edge> Properties { get; } = [];

        public bool CanFail { get; set; }
    }

    private sealed record Edge(IPropertySymbol Property, bool HasOwnRule, bool IsCollection, Node? Target);

    private sealed class Walker(Compilation compilation, CancellationToken cancellationToken)
    {
        private readonly Dictionary<ITypeSymbol, Node> _nodes = new(SymbolEqualityComparer.Default);
        private readonly List<Node> _order = [];
        private readonly INamedTypeSymbol? _validationAttribute =
            compilation.GetTypeByMetadataName(ValidationAttributeName);

        private readonly INamedTypeSymbol? _enumerable =
            compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable);

        public Node? Visit(ITypeSymbol type)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Unwrap(type) is not { } named)
            {
                return null;
            }

            if (_nodes.TryGetValue(named, out Node? existing))
            {
                return existing;
            }

            Node node = new(named, HasTypeRule(named));
            _nodes.Add(named, node);
            _order.Add(node);

            foreach (IPropertySymbol property in PropertiesOf(named))
            {
                bool isCollection = IsCollection(property.Type);
                ITypeSymbol? target = isCollection ? ItemType(property.Type) : property.Type;
                node.Properties.Add(new Edge(
                    property,
                    HasPropertyRule(property),
                    isCollection,
                    target is null ? null : Visit(target)));
            }

            return node;
        }

        public EquatableArray<TypeModel> Result()
        {
            // A rule anywhere below makes a type fail-able; repeat until nothing changes, which
            // settles cycles too.
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (Node node in _order.Where(node => !node.CanFail))
                {
                    node.CanFail = node.HasOwnRule
                        || node.Properties.Any(edge => edge.HasOwnRule || edge.Target?.CanFail == true);
                    changed |= node.CanFail;
                }
            }

            return new EquatableArray<TypeModel>(
            [
                .. _order.Select(node => new TypeModel(
                    node.Type.ToDisplayString(SymbolNames.TypeOf),
                    node.CanFail,
                    new EquatableArray<PropertyModel>(
                    [
                        .. node.Properties
                            .Where(edge => edge.HasOwnRule || edge.Target?.CanFail == true)
                            .Select(edge => new PropertyModel(
                                Escape(edge.Property.Name),
                                edge.Property.MetadataName,
                                edge.Property.ContainingType.ToDisplayString(SymbolNames.TypeOf),
                                edge.IsCollection)),
                    ]))),
            ]);
        }

        private static string Escape(string name) =>
            SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

        private static bool IsSystem(INamespaceSymbol? space)
        {
            INamespaceSymbol? current = space;
            while (current is { IsGlobalNamespace: false })
            {
                if (current.ContainingNamespace is { IsGlobalNamespace: true } && current.Name == "System")
                {
                    return true;
                }

                current = current.ContainingNamespace;
            }

            return false;
        }

        /// <summary>
        /// Public instance properties with a public getter, the most derived declaration of each
        /// name first, then the base types', as Microsoft.Extensions.Validation validates a type's
        /// own members before its bases'.
        /// </summary>
        private static IEnumerable<IPropertySymbol> PropertiesOf(INamedTypeSymbol type)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            for (INamedTypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                IEnumerable<IPropertySymbol> readable = current.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(property => IsReadable(property) && seen.Add(property.Name));
                foreach (IPropertySymbol property in readable)
                {
                    yield return property;
                }
            }
        }

        private static bool IsReadable(IPropertySymbol property) =>
            !property.IsStatic
            && !property.IsIndexer
            && property.DeclaredAccessibility == Accessibility.Public
            && property.GetMethod?.DeclaredAccessibility == Accessibility.Public;

        private static ITypeSymbol? ItemType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return array.ElementType;
            }

            return new[] { type }.Concat(type.AllInterfaces)
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault(item => item.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                ?.TypeArguments[0];
        }

        /// <summary>
        /// The type to walk, or null for one that is never walked.
        /// </summary>
        private INamedTypeSymbol? Unwrap(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            {
                type = nullable.TypeArguments[0];
            }

            if (type is not INamedTypeSymbol named
                || named.TypeKind is not (TypeKind.Class or TypeKind.Struct)
                || named.SpecialType != SpecialType.None
                || named.IsUnboundGenericType
                || IsSystem(named.ContainingNamespace)
                || IsCollection(named)
                || !compilation.IsSymbolAccessibleWithin(named, compilation.Assembly))
            {
                return null;
            }

            return named;
        }

        private bool IsCollection(ITypeSymbol type) =>
            type.SpecialType != SpecialType.System_String
            && (type is IArrayTypeSymbol
                || SymbolEqualityComparer.Default.Equals(type, _enumerable)
                || type.AllInterfaces.Contains(_enumerable, SymbolEqualityComparer.Default));

        private bool HasTypeRule(INamedTypeSymbol type)
        {
            if (type.AllInterfaces.Any(item => ValidatableObjectNames.Contains(item.ToDisplayString())))
            {
                return true;
            }

            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (current.GetAttributes().Any(IsValidationAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPropertyRule(IPropertySymbol property)
        {
            for (IPropertySymbol? current = property; current is not null; current = current.OverriddenProperty)
            {
                if (current.GetAttributes().Any(IsValidationAttribute))
                {
                    return true;
                }
            }

            IParameterSymbol? parameter = property.ContainingType.InstanceConstructors
                .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public)
                .Select(constructor => constructor.Parameters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, property.Name, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(candidate => candidate is not null);
            return parameter?.GetAttributes().Any(IsValidationAttribute) == true;
        }

        private bool IsValidationAttribute(AttributeData attribute)
        {
            for (INamedTypeSymbol? current = attribute.AttributeClass; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, _validationAttribute))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
