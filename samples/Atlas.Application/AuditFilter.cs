using Atlas.Application.Cities;

namespace Atlas.Application;

/// <summary>
/// Records every use case that reached its handler, and how it ended: a filter runs for all of
/// them, after validation. A filter that stops a use case itself returns an ambient error
/// (<see cref="Forbidden"/>, say), the only kind a handler declaring its errors accepts.
/// </summary>
public sealed class AuditFilter(CityStore store) : IUseCaseFilter
{
    public async ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next)
    {
        Result result = await next(context);
        store.Audit($"{context.UseCaseType.Name}: {result.Error?.Code ?? "success"}");
        return result;
    }
}
