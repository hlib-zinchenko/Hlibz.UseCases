using Microsoft.Extensions.Options;

namespace Hlibz.UseCases;

/// <summary>
/// Runs a use case through validation and the registered <see cref="IUseCaseFilter"/>s, then its
/// handler. The pipeline the source generator wraps every handler in calls it; clients never do.
/// Scoped.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class UseCasePipeline
{
    private readonly IServiceProvider _services;
    private readonly IUseCaseValidator _validator;
    private readonly IUseCaseFilter[] _filters;
    private readonly UseCaseValidationOptions _validation;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseCasePipeline"/> class.
    /// </summary>
    /// <param name="services">The scope's services.</param>
    /// <param name="validator">Validates use cases.</param>
    /// <param name="filters">The filters, in registration order.</param>
    /// <param name="options">The options.</param>
    public UseCasePipeline(
        IServiceProvider services,
        IUseCaseValidator validator,
        IEnumerable<IUseCaseFilter> filters,
        IOptions<UseCaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(options);

        _services = services;
        _validator = validator;
        _filters = [.. filters];
        _validation = options.Value.Validation;
    }

    /// <summary>
    /// Handles a use case that returns a value.
    /// </summary>
    /// <typeparam name="TUseCase">The use case type.</typeparam>
    /// <typeparam name="TResult">What it returns.</typeparam>
    /// <param name="useCase">The use case to handle.</param>
    /// <param name="handler">The handler (not its pipeline).</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The handler's result, or the failure validation or a filter returned.</returns>
    public async Task<Result<TResult>> InvokeAsync<TUseCase, TResult>(
        TUseCase useCase,
        IUseCaseHandler<TUseCase, TResult> handler,
        CancellationToken cancellationToken)
        where TUseCase : IUseCase
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(handler);

        if (await ValidateAsync(useCase, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return Result<TResult>.FromFailed(invalid);
        }

        if (_filters.Length == 0)
        {
            return await handler.HandleAsync(useCase, cancellationToken).ConfigureAwait(false);
        }

        Result result = await RunFiltersAsync(
            Context(useCase, typeof(TUseCase), handler, cancellationToken),
            async context => await handler
                .HandleAsync(useCase, context.CancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);

        return result switch
        {
            Result<TResult> typed => typed,
            { IsSuccess: true } => throw SuccessWithoutHandler(handler),
            _ => Result<TResult>.FromFailed(result),
        };
    }

    /// <summary>
    /// Handles a use case that returns no value.
    /// </summary>
    /// <typeparam name="TUseCase">The use case type.</typeparam>
    /// <param name="useCase">The use case to handle.</param>
    /// <param name="handler">The handler (not its pipeline).</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The handler's result, or the failure validation or a filter returned.</returns>
    public async Task<Result> InvokeAsync<TUseCase>(
        TUseCase useCase,
        IUseCaseHandler<TUseCase> handler,
        CancellationToken cancellationToken)
        where TUseCase : IUseCase
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(handler);

        if (await ValidateAsync(useCase, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        if (_filters.Length == 0)
        {
            return await handler.HandleAsync(useCase, cancellationToken).ConfigureAwait(false);
        }

        return await RunFiltersAsync(
            Context(useCase, typeof(TUseCase), handler, cancellationToken),
            async context => await handler
                .HandleAsync(useCase, context.CancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static InvalidOperationException SuccessWithoutHandler(object handler) =>
        new(
            $"A use case filter returned a success without calling next for {handler.GetType()}, "
            + "so there is no value to return. A filter either calls next or returns a failure.");

    private UseCaseContext Context(
        IUseCase useCase,
        Type useCaseType,
        object handler,
        CancellationToken cancellationToken) =>
        new(useCase, useCaseType, handler.GetType(), _services, cancellationToken);

    private async ValueTask<Result?> ValidateAsync(IUseCase useCase, CancellationToken cancellationToken)
    {
        if (!_validation.Enabled)
        {
            return null;
        }

        IReadOnlyDictionary<string, string[]>? errors = await _validator
            .ValidateAsync(useCase, cancellationToken)
            .ConfigureAwait(false);
        if (errors is null)
        {
            return null;
        }

        return _validation.FailureStatus == ResultStatus.Error
            ? Result.Error(errors, _validation.FailureMessage)
            : Result.Invalid(errors, _validation.FailureMessage);
    }

    private ValueTask<Result> RunFiltersAsync(
        UseCaseContext context,
        UseCaseFilterDelegate handle)
    {
        return Next(0, context);

        ValueTask<Result> Next(int index, UseCaseContext current) =>
            index == _filters.Length
                ? handle(current)
                : _filters[index].InvokeAsync(current, next => Next(index + 1, next));
    }
}
