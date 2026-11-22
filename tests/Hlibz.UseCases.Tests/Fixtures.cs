namespace Hlibz.UseCases.Tests;

public sealed record CreateCityCommand(
    [Required, MaxLength(10)] string Name,
    [Range(1, 1000)] int MagnitudeKm,
    Household Household,
    IReadOnlyList<CostLine> Costs) : IUseCase, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MagnitudeKm > 500 && Name == "Big")
        {
            yield return new ValidationResult("Big cities stay under 500.", [nameof(MagnitudeKm)]);
        }
    }

    public static CreateCityCommand Valid(string name = "Kyiv") =>
        new(name, 100, new Household(2, 1), [new CostLine(10, "RENT")]);
}

public sealed record Household([Range(1, 10)] int Adults, [Range(0, 10)] int Children);

public sealed record CostLine([Range(0, 1_000_000)] decimal Value, [UpperCase] string Note);

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class UpperCaseAttribute : ValidationAttribute, IDescribedRule
{
    public const string Text = "Must be in capitals.";

    public override bool IsValid(object? value) =>
        value is string text && !text.Any(char.IsLower);

    public void Describe(RuleDescription rule)
    {
        rule.Pattern = "^[^a-z]*$";
        rule.AddText(Text);
    }
}

public sealed record CityDto(string Name);

public interface ICreateCityCommandHandler : IUseCaseHandler<CreateCityCommand, CityDto>;

internal sealed class CreateCityCommandHandler(CallLog log) : ICreateCityCommandHandler
{
    public Task<Result<CityDto>> HandleAsync(
        CreateCityCommand useCase,
        CancellationToken cancellationToken = default)
    {
        log.Add("handler");
        return Task.FromResult(Result<CityDto>.Success(new CityDto(useCase.Name)));
    }
}

public sealed record DeleteCityCommand([Range(1, int.MaxValue)] int Id) : IUseCase;

public interface IDeleteCityCommandHandler : IUseCaseHandler<DeleteCityCommand>;

internal sealed class DeleteCityCommandHandler(CallLog log) : IDeleteCityCommandHandler
{
    public Task<Result> HandleAsync(DeleteCityCommand useCase, CancellationToken cancellationToken = default)
    {
        log.Add("handler");
        return Task.FromResult(useCase.Id == 404 ? Result.NotFound() : Result.Success());
    }
}

/// <summary>A use case with nothing to validate, handled without an interface of its own.</summary>
public sealed record PingQuery(string? Echo) : IUseCase;

internal sealed class PingQueryHandler : IUseCaseHandler<PingQuery, string?>
{
    public Task<Result<string?>> HandleAsync(PingQuery useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<string?>.Success(useCase.Echo));
}

public sealed class CallLog
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string entry) => _entries.Add(entry);
}

internal sealed class RecordingFilter(CallLog log, string name) : IUseCaseFilter
{
    public async ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next)
    {
        log.Add($"{name}:before:{context.UseCaseType.Name}");
        Result result = await next(context);
        log.Add($"{name}:after:{result.Status}");
        return result;
    }
}

internal sealed class FirstFilter(CallLog log) : IUseCaseFilter
{
    private readonly RecordingFilter _inner = new(log, "first");

    public ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next) =>
        _inner.InvokeAsync(context, next);
}

internal sealed class SecondFilter(CallLog log) : IUseCaseFilter
{
    private readonly RecordingFilter _inner = new(log, "second");

    public ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next) =>
        _inner.InvokeAsync(context, next);
}

internal sealed class ForbiddingFilter : IUseCaseFilter
{
    public ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next) =>
        ValueTask.FromResult(Result.Forbidden("Not for you."));
}

internal sealed class SuccessWithoutNextFilter : IUseCaseFilter
{
    public ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next) =>
        ValueTask.FromResult(Result.Success());
}

internal static class Services
{
    public static ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<CallLog>();
        services.AddUseCases([typeof(Services).Assembly]);
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
