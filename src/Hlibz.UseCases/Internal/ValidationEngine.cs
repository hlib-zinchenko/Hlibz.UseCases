using System.Collections;

namespace Hlibz.UseCases.Internal;

/// <summary>
/// Validates a value against its DataAnnotations rules, walking the tables the source generator
/// wrote. The order and the error keys follow Microsoft.Extensions.Validation (the engine behind
/// ASP.NET Core's <c>AddValidation()</c>), so a use case fails the same way in either place.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Each property: <see cref="RequiredAttribute"/> first, and nothing else when it fails;
/// then the other attributes; then the value itself, item by item for a collection
/// (<c>Costs[0]</c>), each validated as its runtime type when that type has a rule.</item>
/// <item>Then, only when no property failed: the type's own attributes, and only when those
/// passed too, <see cref="IValidatableObject"/>.</item>
/// <item>A property's errors are keyed by its path; a type attribute's or an
/// <see cref="IValidatableObject"/> result's by the path plus each member name it gives. A
/// type attribute result naming no member is keyed by the object's path, an
/// <see cref="IValidatableObject"/> one by <c>""</c>, wherever the object sits (as
/// Microsoft.Extensions.Validation does).</item>
/// <item>On .NET 11, <c>AsyncValidationAttribute</c>s run only after the synchronous ones passed,
/// one at a time, stopping at the first failure, and <c>IAsyncValidatableObject</c> is used when
/// implemented.</item>
/// <item>Messages are the attributes' own: there is no localization. A property is named in them
/// by its <see cref="DisplayAttribute"/>, else by
/// <see cref="UseCaseValidationOptions.DisplayNamePolicy"/> when set, else as declared.</item>
/// </list>
/// </remarks>
internal sealed class ValidationEngine
{
    private readonly UseCaseRegistry _registry;
    private readonly int _maxDepth;
    private readonly JsonNamingPolicy? _displayNamePolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationEngine"/> class.
    /// </summary>
    /// <param name="registry">The validatable types.</param>
    /// <param name="options">How deep to go before giving up, and how to name properties.</param>
    public ValidationEngine(UseCaseRegistry registry, UseCaseValidationOptions options)
    {
        _registry = registry;
        _maxDepth = options.MaxDepth;
        _displayNamePolicy = options.DisplayNamePolicy;
    }

    /// <summary>
    /// Validates <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="services">The services validation attributes can resolve.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The errors keyed by path, or <see langword="null"/> when there are none.</returns>
    public async ValueTask<IReadOnlyDictionary<string, string[]>?> ValidateAsync(
        object value,
        IServiceProvider? services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!_registry.TryGetValidatableType(value.GetType(), out ValidatableType? type))
        {
            return null;
        }

        Run run = new(services, cancellationToken);
        await ValidateTypeAsync(value, type, run).ConfigureAwait(false);
        return run.Errors.Count == 0
            ? null
            : run.Errors.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToArray(),
                StringComparer.Ordinal);
    }

    private static string Join(string prefix, string name) =>
        prefix.Length == 0 ? name : $"{prefix}.{name}";

    private static void ReportForMembers(
        Run run,
        ValidationResult result,
        string prefix,
        string keyWithoutMembers)
    {
        if (result.ErrorMessage is not { } message)
        {
            return;
        }

        bool any = false;
        foreach (string member in result.MemberNames)
        {
            any = true;
            run.Add(Join(prefix, member), message);
        }

        if (!any)
        {
            run.Add(keyWithoutMembers, message);
        }
    }

    private static bool Failed(ValidationResult? result) =>
        result is not null && result != ValidationResult.Success;

    // Synchronous attributes first; asynchronous ones (.NET 11) only once those all passed.
    private static async ValueTask ValidateAttributesAsync(
        ValidationAttribute[] attributes,
        object? value,
        ValidationContext context,
        Run run,
        Action<ValidationResult> report)
    {
        bool failed = false;
        foreach (ValidationAttribute attribute in attributes)
        {
            if (attribute is AsyncValidationAttribute)
            {
                continue;
            }

            ValidationResult? result = attribute.GetValidationResult(value, context);
            if (Failed(result))
            {
                failed = true;
                report(result!);
            }
        }

        if (failed)
        {
            return;
        }

        foreach (AsyncValidationAttribute attribute in attributes.OfType<AsyncValidationAttribute>())
        {
            ValidationResult? result = await attribute
                .GetValidationResultAsync(value, context, run.CancellationToken)
                .ConfigureAwait(false);
            if (Failed(result))
            {
                report(result!);
                return;
            }
        }
    }

    private static async ValueTask ValidateObjectAsync(
        object value,
        ValidationContext context,
        Run run,
        string path)
    {
        if (value is IAsyncValidatableObject asyncValidatable)
        {
            await foreach (ValidationResult result in asyncValidatable
                               .ValidateAsync(context, run.CancellationToken)
                               .ConfigureAwait(false))
            {
                ReportObjectResult(run, result, path);
            }

            return;
        }

        if (value is IValidatableObject validatable)
        {
            foreach (ValidationResult result in validatable.Validate(context))
            {
                ReportObjectResult(run, result, path);
            }
        }
    }

    private static void ReportObjectResult(Run run, ValidationResult result, string path)
    {
        if (result != ValidationResult.Success)
        {
            ReportForMembers(run, result, path, keyWithoutMembers: string.Empty);
        }
    }

    private async ValueTask ValidateTypeAsync(object value, ValidatableType type, Run run)
    {
        CheckDepth(run, type.Type.Name);
        int before = run.Errors.Count;

        foreach (ValidatableProperty property in type.Properties)
        {
            run.CancellationToken.ThrowIfCancellationRequested();
            await ValidatePropertyAsync(value, property, run).ConfigureAwait(false);
        }

        if (run.Errors.Count > before)
        {
            return;
        }

        string path = run.Path;
        ValidationContext context = new(value, type.Type.Name, run.Services, items: null);
        await ValidateAttributesAsync(
            RuleAttributes.For(type.Type),
            value,
            context,
            run,
            result => ReportForMembers(run, result, path, path)).ConfigureAwait(false);

        if (run.Errors.Count > before)
        {
            return;
        }

        await ValidateObjectAsync(value, context, run, path).ConfigureAwait(false);
    }

    private async ValueTask ValidatePropertyAsync(
        object container,
        ValidatableProperty property,
        Run run)
    {
        object? value = property.GetValue(container);
        PropertyRules rules = RuleAttributes.For(property);
        string original = run.Path;
        run.Path = Join(original, property.Name);
        string path = run.Path;

        string displayName = rules.Display?.GetName()
            ?? _displayNamePolicy?.ConvertName(property.Name)
            ?? property.Name;
        ValidationContext context = new(
            container,
            displayName,
            run.Services,
            items: null)
        {
            MemberName = property.Name,
        };

        try
        {
            if (rules.Required is { } required)
            {
                ValidationResult? result = required.GetValidationResult(value, context);
                if (Failed(result))
                {
                    Report(run, path, result!);
                    return;
                }
            }

            await ValidateAttributesAsync(
                rules.Attributes,
                value,
                context,
                run,
                result => Report(run, path, result)).ConfigureAwait(false);

            CheckDepth(run, $"{property.DeclaringType.Name}.{property.Name}");
            run.Depth++;
            try
            {
                await ValidateValueAsync(value, property.IsCollection, run).ConfigureAwait(false);
            }
            finally
            {
                run.Depth--;
            }
        }
        finally
        {
            run.Path = original;
        }

        static void Report(Run run, string path, ValidationResult result)
        {
            if (result.ErrorMessage is { } message)
            {
                run.Add(path, message);
            }
        }
    }

    private async ValueTask ValidateValueAsync(object? value, bool isCollection, Run run)
    {
        if (isCollection && value is IEnumerable items)
        {
            string prefix = run.Path;
            int index = 0;
            foreach (object? item in items)
            {
                if (item is not null
                    && _registry.TryGetValidatableType(item.GetType(), out ValidatableType? itemType))
                {
                    run.Path = $"{prefix}[{index}]";
                    await ValidateTypeAsync(item, itemType, run).ConfigureAwait(false);
                }

                index++;
            }

            run.Path = prefix;
        }
        else if (value is not null
                 && _registry.TryGetValidatableType(value.GetType(), out ValidatableType? valueType))
        {
            await ValidateTypeAsync(value, valueType, run).ConfigureAwait(false);
        }
    }

    private void CheckDepth(Run run, string where)
    {
        if (run.Depth >= _maxDepth)
        {
            throw new InvalidOperationException(
                $"Maximum validation depth of {_maxDepth} exceeded at '{run.Path}' in '{where}'. "
                + "This is likely caused by a circular reference in the object graph. Raise "
                + "UseCaseValidationOptions.MaxDepth if deeper validation is required.");
        }
    }

    /// <summary>
    /// One validation: where it is, and what it found.
    /// </summary>
    private sealed class Run(IServiceProvider? services, CancellationToken cancellationToken)
    {
        public IServiceProvider? Services { get; } = services;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public Dictionary<string, List<string>> Errors { get; } = new(StringComparer.Ordinal);

        public string Path { get; set; } = string.Empty;

        public int Depth { get; set; }

        public void Add(string key, string message)
        {
            if (!Errors.TryGetValue(key, out List<string>? messages))
            {
                messages = [];
                Errors.Add(key, messages);
            }

            messages.Add(message);
        }
    }
}
