namespace Atlas.Application.Cities;

internal sealed class CreateCityCommandHandler(CityStore store) : ICreateCityCommandHandler
{
    public static readonly ErrorCode NameTaken = new("atlas_1001", "A city with this name already exists in the country.");

    public Task<Result<CityDto>> HandleAsync(
        CreateCityCommand useCase,
        CancellationToken cancellationToken = default)
    {
        string name = useCase.Name.Trim();
        if (store.NameTaken(name, useCase.CountryCode))
        {
            return Task.FromResult(Result<CityDto>.Conflict(NameTaken));
        }

        return Task.FromResult(Result<CityDto>.Success(
            store.Add(name, useCase.CountryCode, useCase.Population)));
    }
}
