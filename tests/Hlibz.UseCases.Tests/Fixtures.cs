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
        return Task.FromResult(useCase.Id == 404 ? new CityNotFound(useCase.Id) : Result.Success());
    }
}

[Error("tests_1001", ResultStatus.NotFound, "The city doesn't exist.")]
public sealed record CityNotFound(int CityId) : Error;

[Error("tests_1002", ResultStatus.Conflict, "Another city has the name.")]
public sealed record CityNameTaken(string Name) : Error;

[Error("tests_1003", ResultStatus.Error, "An error no use case declares.")]
public sealed record UndeclaredError : Error;

// A union compiles to a struct without equality, which CA1815 flags.
#pragma warning disable CA1815
public union RenameCityError(CityNotFound, CityNameTaken);
#pragma warning restore CA1815

/// <summary>A use case declaring its errors: 404 is missing, "Lviv" is taken.</summary>
public sealed record RenameCityCommand([Range(1, int.MaxValue)] int Id, [Required, MaxLength(10)] string Name) : IUseCase;

public interface IRenameCityCommandHandler : IUseCaseHandler<RenameCityCommand, CityDto, RenameCityError>;

internal sealed class RenameCityCommandHandler(CallLog log) : IRenameCityCommandHandler
{
    public Task<Result<CityDto, RenameCityError>> HandleAsync(
        RenameCityCommand useCase,
        CancellationToken cancellationToken = default)
    {
        log.Add("handler");
        return Task.FromResult<Result<CityDto, RenameCityError>>(useCase switch
        {
            { Id: 404 } => new CityNotFound(useCase.Id),
            { Name: "Lviv" } => new CityNameTaken(useCase.Name),
            _ => new CityDto(useCase.Name),
        });
    }
}

/// <summary>A use case declaring its errors that returns no value.</summary>
public sealed record ArchiveCityCommand(int Id) : IUseCase;

internal sealed class ArchiveCityCommandHandler : IUseCaseHandler<ArchiveCityCommand, Unit, RenameCityError>
{
    public Task<Result<Unit, RenameCityError>> HandleAsync(
        ArchiveCityCommand useCase,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Result<Unit, RenameCityError>>(
            useCase.Id == 404 ? new CityNotFound(useCase.Id) : Unit.Value);
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
        ValueTask.FromResult<Result>(new Forbidden());
}

internal sealed class UndeclaredErrorFilter : IUseCaseFilter
{
    public ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next) =>
        ValueTask.FromResult<Result>(new UndeclaredError());
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
