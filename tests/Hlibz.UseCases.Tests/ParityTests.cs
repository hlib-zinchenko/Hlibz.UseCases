using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;

namespace Hlibz.UseCases.Tests;

/// <summary>
/// The generated validation must fail exactly as Microsoft.Extensions.Validation (ASP.NET Core's
/// AddValidation()) does: same keys, same messages, same order.
/// </summary>
public sealed class ParityTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public ParityTests()
    {
        ServiceCollection services = new();
        services.AddSingleton<CallLog>();
        services.AddUseCases([typeof(ParityTests).Assembly]);
        services.AddValidation();
        _provider = services.BuildServiceProvider();
    }

    public static TheoryData<string, object> Models => new()
    {
        { "required missing", new RequiredFirst(null) },
        { "required empty", new RequiredFirst(string.Empty) },
        { "min length", new RequiredFirst("ab") },
        { "valid", new RequiredFirst("abc") },
        { "many rules", new ManyRules("AB") },
        {
            "nested and collections",
            new Order(
                0,
                new Line(null, 0),
                [new Line("a", 1), null, new Line(null, 500)],
                [new Line("b", 0)],
                new Dictionary<string, Line> { ["x"] = new(null, 0) })
        },
        { "null nested", new Order(5, null, [], [], new Dictionary<string, Line>()) },
        { "object rule with members", new WholeObjectRules(5, 3, new Window(1, 2)) },
        { "object rule without members", new WholeObjectRules(7, 9, new Window(1, 2)) },
        { "property failure skips object rule", new WholeObjectRules(11, 3, new Window(1, 2)) },
        { "nested object rule", new WholeObjectRules(1, 2, new Window(5, 1)) },
        { "type rule with member", new TypeRuleFirst(null, null, new Window(1, 2)) },
        { "type rule without member", new TypeRuleFirst("both", "x", new Window(1, 2)) },
        { "type rule passed", new TypeRuleFirst("a", null, new Window(1, 2)) },
        { "nested failure skips type rule", new TypeRuleFirst(null, null, new Window(5, 1)) },
        { "display names", new Displayed { Name = null, Age = 5 } },
        { "inherited", new Derived(null, 9) },
        { "compare", new Passwords { Password = "a", Confirm = "b" } },
        { "deep", new Deep([new Level1(new Level2([new Line("a", 1), new Line(null, 1)]))]) },
        { "struct", new WithStruct(new Money(-1, null), new Money(5000, "USD")) },
    };

    [Theory]
    [MemberData(nameof(Models))]
    public async Task Validate_WithModel_FailsAsMicrosoftExtensionsValidationDoes(string name, object model)
    {
        _ = name;
        using IServiceScope scope = _provider.CreateScope();

        IReadOnlyDictionary<string, string[]>? ours = await scope.ServiceProvider
            .GetRequiredService<IUseCaseValidator>()
            .ValidateAsync(model, TestContext.Current.CancellationToken);
        IReadOnlyDictionary<string, string[]>? theirs = await ValidateWithMicrosoftAsync(model, scope.ServiceProvider);

        Assert.Equal(Flatten(theirs), Flatten(ours));
    }

    [Fact]
    public async Task Validate_WithInvalidRoot_ReportsEveryNestedModel()
    {
        using IServiceScope scope = _provider.CreateScope();
        ParityRoot root = new(
            new RequiredFirst(null),
            new ManyRules("ab"),
            new Order(5, null, [], [], new Dictionary<string, Line>()),
            new WholeObjectRules(1, 2, new Window(1, 2)),
            new TypeRuleFirst("a", null, new Window(1, 2)),
            new Displayed { Name = "a", Age = 30 },
            new Derived("x", 1),
            new Passwords { Password = "a", Confirm = "a" },
            new Deep([]),
            new WithStruct(new Money(1, "USD"), null));

        IReadOnlyDictionary<string, string[]>? ours = await scope.ServiceProvider
            .GetRequiredService<IUseCaseValidator>()
            .ValidateAsync(root, TestContext.Current.CancellationToken);
        IReadOnlyDictionary<string, string[]>? theirs = await ValidateWithMicrosoftAsync(root, scope.ServiceProvider);

        // TypeRuleFirst's IValidatableObject always fails, naming no member: keyed "" at any depth.
        Assert.Equal(["RequiredFirst.Name", "ManyRules.Code", string.Empty], ours!.Keys);
        Assert.Equal(Flatten(theirs), Flatten(ours));
    }

    public void Dispose() => _provider.Dispose();

    private static string[] Flatten(IReadOnlyDictionary<string, string[]>? errors) =>
        errors is null
            ? []
            : [.. errors.SelectMany(pair => pair.Value.Select(message => $"{pair.Key} => {message}"))];

    private static async Task<IReadOnlyDictionary<string, string[]>?> ValidateWithMicrosoftAsync(
        object model,
        IServiceProvider services)
    {
        ValidationOptions options = services.GetRequiredService<IOptions<ValidationOptions>>().Value;
        Assert.True(options.TryGetValidatableTypeInfo(model.GetType(), out IValidatableTypeInfo? info));
        ValidateContext context = new() { ValidationOptions = options, ServiceProvider = services };
        await info.ValidateAsync(model, context, TestContext.Current.CancellationToken);
        return context.ValidationErrors is not { Count: > 0 } errors
            ? null
            : errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
    }
}
