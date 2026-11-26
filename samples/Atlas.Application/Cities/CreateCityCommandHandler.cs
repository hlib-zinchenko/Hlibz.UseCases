namespace Atlas.Application.Cities;

internal sealed class CreateCityCommandHandler(CityStore store) : ICreateCityCommandHandler
{
    public Task<Result<CityDto, CreateCityError>> HandleAsync(
        CreateCityCommand useCase,
        CancellationToken cancellationToken = default)
    {
        string name = useCase.Name.Trim();
        if (store.FindByName(name, useCase.CountryCode) is { } namesake)
        {
            return Task.FromResult<Result<CityDto, CreateCityError>>(new CityNameTaken(namesake.Id));
        }

        return Task.FromResult<Result<CityDto, CreateCityError>>(
            store.Add(name, useCase.CountryCode, useCase.Population));
    }
}
