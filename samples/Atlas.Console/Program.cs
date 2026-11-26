using System.Globalization;

using Atlas.Application;
using Atlas.Application.Cities;

using Hlibz.Results;

using Microsoft.Extensions.DependencyInjection;

// Usage: Atlas.Console get <id>
//        Atlas.Console create <name> <country code> <population>
// Same use cases, same validation and audit as Atlas.Api: only the transport differs.
ServiceCollection services = new();
services.AddAtlasApplication();
await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
await using AsyncServiceScope scope = provider.CreateAsyncScope();

Result? result = args switch
{
    ["get", string id] => await scope.ServiceProvider
        .GetRequiredService<IGetCityQueryHandler>()
        .HandleAsync(new GetCityQuery(int.Parse(id, CultureInfo.InvariantCulture))),
    ["create", string name, string country, string population] => await scope.ServiceProvider
        .GetRequiredService<ICreateCityCommandHandler>()
        .HandleAsync(new CreateCityCommand(name, country, int.Parse(population, CultureInfo.InvariantCulture), Districts: null)),
    _ => null,
};

Console.WriteLine(result switch
{
    null => "Usage: get <id> | create <name> <country code> <population>",
    Result<CityDto> { IsSuccess: true, Value: var city } => $"{city.Id}: {city.Name}, {city.CountryCode} ({city.Population:N0})",

    // An ambient error: the pipeline rejected the use case before its handler ran.
    { Error: ValidationFailed failed } => string.Join(
        Environment.NewLine,
        failed.Errors.Select(pair => $"{pair.Key}: {string.Join(" ", pair.Value)}").Prepend("Invalid:")),

    // A declared error, with its data.
    Result<CityDto, CreateCityError> { DeclaredError: CityNameTaken taken } => $"City {taken.CityId} already has that name.",
    Result<CityDto, GetCityError> { DeclaredError: CityNotFound missing } => $"No city {missing.CityId}.",
    _ => result.ToString(),
});

foreach (string entry in provider.GetRequiredService<CityStore>().AuditLog)
{
    Console.WriteLine($"audit: {entry}");
}

return result is { IsSuccess: true } ? 0 : 1;
