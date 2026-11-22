namespace Atlas.Application.Cities;

internal sealed class GetCityQueryHandler(CityStore store) : IGetCityQueryHandler
{
    public Task<Result<CityDto>> HandleAsync(GetCityQuery useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.Find(useCase.Id) is { } city
            ? Result<CityDto>.Success(city)
            : Result<CityDto>.NotFound());
}
