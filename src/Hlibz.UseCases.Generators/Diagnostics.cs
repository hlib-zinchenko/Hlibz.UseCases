using Microsoft.CodeAnalysis;

namespace Hlibz.UseCases.Generators;

/// <summary>
/// The build errors and warnings: handlers the generator can't register.
/// </summary>
internal static class Diagnostics
{
    /// <summary>
    /// HUC001: the handler handles no use case, or more than one.
    /// </summary>
    public static readonly DiagnosticDescriptor NotOneUseCase = Error(
        "HUC001",
        "Handler must handle exactly one use case",
        Messages.NotOneUseCaseText);

    /// <summary>
    /// HUC002: a type the generated code has to name isn't accessible to it.
    /// </summary>
    public static readonly DiagnosticDescriptor Inaccessible = Error(
        "HUC002",
        "Type isn't accessible to the generated code",
        Messages.InaccessibleText);

    /// <summary>
    /// HUC003: the handler is generic, or nested in a generic type, so it is never registered. A
    /// warning: a generic class can be a base for handlers, which <c>abstract</c> says.
    /// </summary>
    public static readonly DiagnosticDescriptor GenericHandler = Warning(
        "HUC003",
        "Generic handler is never registered",
        Messages.GenericHandlerText);

    /// <summary>
    /// HUC004: the handler's own interface declares members, which the generated pipeline can't
    /// implement.
    /// </summary>
    public static readonly DiagnosticDescriptor InterfaceWithMembers = Error(
        "HUC004",
        "Handler interface declares members of its own",
        Messages.InterfaceWithMembersText);

    /// <summary>
    /// HUC005: two handlers in the assembly handle the same use case.
    /// </summary>
    public static readonly DiagnosticDescriptor ManyHandlers = Error(
        "HUC005",
        "Use case has more than one handler",
        "{0} is handled by both {1} and {2}; a use case has exactly one handler");

    /// <summary>
    /// HUC006: the handler is a struct.
    /// </summary>
    public static readonly DiagnosticDescriptor StructHandler = Error(
        "HUC006",
        "Handler is a struct",
        "{0} is a struct; a handler must be a class");

    /// <summary>
    /// HUC007: the handler has no public constructor for the container to call.
    /// </summary>
    public static readonly DiagnosticDescriptor NoPublicConstructor = Error(
        "HUC007",
        "Handler has no public constructor",
        Messages.NoPublicConstructorText);

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        Descriptor(id, title, message, DiagnosticSeverity.Error);

    private static DiagnosticDescriptor Warning(string id, string title, string message) =>
        Descriptor(id, title, message, DiagnosticSeverity.Warning);

    private static DiagnosticDescriptor Descriptor(
        string id,
        string title,
        string message,
        DiagnosticSeverity severity) =>
        new(
            id,
            title,
            message,
            Messages.Category,
            severity,
            isEnabledByDefault: true,
            helpLinkUri: Messages.HelpLink);

    /// <summary>
    /// The longer texts, kept apart so each descriptor reads on a few lines.
    /// </summary>
    private static class Messages
    {
        public const string Category = "Hlibz.UseCases";

        public const string HelpLink =
            "https://github.com/hlib-zinchenko/Hlibz.UseCases#build-errors";

        public const string NotOneUseCaseText =
            "{0} must implement exactly one IUseCaseHandler<TUseCase>, "
            + "IUseCaseHandler<TUseCase, TResult> or IUseCaseHandler<TUseCase, TResult, TError>; "
            + "it implements {1}";

        public const string InaccessibleText =
            "{0} isn't accessible to the generated code; make it, and any type it is nested in, "
            + "internal or public";

        public const string GenericHandlerText =
            "{0} is generic or nested in a generic type, so it is never registered; make it "
            + "abstract if it is a base for handlers, and derive non-generic handlers from it";

        public const string InterfaceWithMembersText =
            "{0} declares members of its own, which the generated pipeline can't implement; a "
            + "handler's interface only derives from IUseCaseHandler<...>";

        public const string NoPublicConstructorText =
            "{0} has no public constructor; the container creates handlers through a public "
            + "constructor (a class's own accessibility can stay internal)";
    }
}
