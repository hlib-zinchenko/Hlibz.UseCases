using Microsoft.Extensions.Options;

#if NET11_0_OR_GREATER
using Microsoft.Extensions.Validation;
#endif

namespace Hlibz.UseCases.Tests;

public sealed class EngineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Validate_WithCircularReference_ThrowsAtMaxDepth()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();
        Chain chain = new() { Value = 1 };
        chain.Next = chain;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await scope.ServiceProvider
                .GetRequiredService<IUseCaseValidator>()
                .ValidateAsync(new ChainCommand(chain), Token));

        Assert.Contains("Maximum validation depth of 32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_WithTypeWithoutRules_ReturnsNull()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();

        IReadOnlyDictionary<string, string[]>? errors = await scope.ServiceProvider
            .GetRequiredService<IUseCaseValidator>()
            .ValidateAsync(new PingQuery(null), Token);

        Assert.Null(errors);
    }

#if NET11_0_OR_GREATER
    [Fact]
    public async Task Validate_WithAsyncRules_RunsThemAfterSynchronousOnesAsMicrosoftDoes()
    {
        ServiceCollection services = new();
        services.AddSingleton<CallLog>();
        services.AddUseCases([typeof(EngineTests).Assembly]);
        services.AddValidation();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IUseCaseValidator validator = scope.ServiceProvider.GetRequiredService<IUseCaseValidator>();

        foreach (AsyncCommand command in new AsyncCommand[] { new("taken"), new("x"), new("free") })
        {
            IReadOnlyDictionary<string, string[]>? ours = await validator.ValidateAsync(command, Token);
            ValidationOptions options = provider.GetRequiredService<IOptions<ValidationOptions>>().Value;
            Assert.True(options.TryGetValidatableTypeInfo(typeof(AsyncCommand), out IValidatableTypeInfo? info));
            ValidateContext context = new() { ValidationOptions = options, ServiceProvider = scope.ServiceProvider };
            await info.ValidateAsync(command, context, Token);

            string[] theirs =
            [
                .. (context.ValidationErrors ?? new Dictionary<string, IReadOnlyList<ValidationError>>())
                    .SelectMany(pair => pair.Value.Select(error => $"{pair.Key} => {error.ErrorMessage}")),
            ];
            Assert.Equal(
                theirs,
                ours?.SelectMany(pair => pair.Value.Select(message => $"{pair.Key} => {message}")) ?? []);
        }
    }
#endif
}

public sealed class Chain
{
    [Range(0, 10)]
    public int Value { get; set; }

    public Chain? Next { get; set; }
}

public sealed record ChainCommand(Chain Chain) : IUseCase;

internal sealed class ChainCommandHandler : IUseCaseHandler<ChainCommand>
{
    public Task<Result> HandleAsync(ChainCommand useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}

#if NET11_0_OR_GREATER
[ValidatableType]
public sealed record AsyncCommand([MinLength(2), NotTaken] string Name) : IUseCase, IAsyncValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => [];

    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext validationContext,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        if (Name == "free")
        {
            yield return new ValidationResult("Free names are reviewed.", [nameof(Name)]);
        }
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotTakenAttribute : AsyncValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        Check(value);

    protected override async Task<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        return Check(value);
    }

    private static ValidationResult? Check(object? value) =>
        value is "taken" ? new ValidationResult("The name is taken.") : ValidationResult.Success;
}

internal sealed class AsyncCommandHandler : IUseCaseHandler<AsyncCommand>
{
    public Task<Result> HandleAsync(AsyncCommand useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}
#endif
