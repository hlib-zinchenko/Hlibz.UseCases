using Microsoft.Extensions.Options;

namespace Hlibz.UseCases;

/// <summary>
/// Runs a use case through validation (an invalid one fails with <see cref="ValidationFailed"/>)
/// and the registered <see cref="IUseCaseFilter"/>s, then its handler. The pipeline the source generator wraps every handler in calls it; clients never do.
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
    /// Handles a use case that declares its errors.
    /// </summary>
    /// <typeparam name="TUseCase">The use case type.</typeparam>
    /// <typeparam name="TResult">What it returns.</typeparam>
    /// <typeparam name="TError">The errors it declares.</typeparam>
    /// <param name="useCase">The use case to handle.</param>
    /// <param name="handler">The handler (not its pipeline).</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The handler's result, or the ambient failure validation or a filter returned.</returns>
    /// <exception cref="InvalidOperationException">A filter returned a success without calling
    /// <c>next</c>, or failed with an error that is neither ambient nor one of the set's.</exception>
    public async Task<Result<TResult, TError>> InvokeAsync<TUseCase, TResult, TError>(
        TUseCase useCase,
        IUseCaseHandler<TUseCase, TResult, TError> handler,
        CancellationToken cancellationToken)
        where TUseCase : IUseCase
        where TError : struct, IUnion
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

        Result result = await RunFiltersAsync(
            Context(useCase, typeof(TUseCase), handler, cancellationToken),
            async context => await handler
                .HandleAsync(useCase, context.CancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);

        return result switch
        {
            Result<TResult, TError> typed => typed,
            { Error: { } error } => FilterFailure<TResult, TError>(error, handler),
            _ => throw SuccessWithoutHandler(handler),
        };
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
    /// <exception cref="InvalidOperationException">A filter returned a success without calling
    /// <c>next</c>.</exception>
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
            return invalid;
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
            { Error: { } error } => Result<TResult>.Failure(error),
            _ => throw SuccessWithoutHandler(handler),
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

    /// <summary>
    /// A filter's own failure as the result of a handler declaring its errors, which accepts only
    /// an ambient error or one of its set.
    /// </summary>
    private static Result<TResult, TError> FilterFailure<TResult, TError>(Error error, object handler)
        where TError : struct, IUnion
    {
        try
        {
            return Result<TResult, TError>.Failure(error);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"A use case filter failed {handler.GetType()} with {error.GetType().Name}, which "
                + $"is neither an {nameof(AmbientError)} nor one of {typeof(TError).Name}'s "
                + "errors. A filter stops with an ambient error (Unauthorized, Forbidden, "
                + "Unavailable).",
                exception);
        }
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

    private async ValueTask<ValidationFailed?> ValidateAsync(IUseCase useCase, CancellationToken cancellationToken)
    {
        if (!_validation.Enabled)
        {
            return null;
        }

        IReadOnlyDictionary<string, string[]>? errors = await _validator
            .ValidateAsync(useCase, cancellationToken)
            .ConfigureAwait(false);
        return errors is null ? null : new ValidationFailed(errors);
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
