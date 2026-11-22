using Atlas.Application.Cities;

namespace Atlas.Application;

/// <summary>
/// Records every use case that reached its handler, and how it ended: a filter runs for all of
/// them, after validation.
/// </summary>
public sealed class AuditFilter(CityStore store) : IUseCaseFilter
{
    public async ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next)
    {
        Result result = await next(context);
        store.Audit($"{context.UseCaseType.Name}: {result.Status}");
        return result;
    }
}
