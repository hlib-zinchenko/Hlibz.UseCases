namespace Atlas.Application.Cities;

internal sealed class GetCityQueryHandler(CityStore store) : IGetCityQueryHandler
{
    public Task<Result<CityDto, GetCityError>> HandleAsync(GetCityQuery useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult<Result<CityDto, GetCityError>>(
            store.Find(useCase.Id) is { } city ? city : new CityNotFound(useCase.Id));
}
