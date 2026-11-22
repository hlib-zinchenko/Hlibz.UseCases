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

Result result = args switch
{
    ["get", string id] => await scope.ServiceProvider
        .GetRequiredService<IGetCityQueryHandler>()
        .HandleAsync(new GetCityQuery(int.Parse(id, CultureInfo.InvariantCulture))),
    ["create", string name, string country, string population] => await scope.ServiceProvider
        .GetRequiredService<ICreateCityCommandHandler>()
        .HandleAsync(new CreateCityCommand(name, country, int.Parse(population, CultureInfo.InvariantCulture), Districts: null)),
    _ => Result.Error("Usage: get <id> | create <name> <country code> <population>"),
};

Console.WriteLine(Describe(result));
foreach (string entry in provider.GetRequiredService<CityStore>().AuditLog)
{
    Console.WriteLine($"audit: {entry}");
}

return result.IsSuccess ? 0 : 1;

static string Describe(Result result) => result switch
{
    Result<CityDto> { IsSuccess: true, Value: var city } => $"{city.Id}: {city.Name}, {city.CountryCode} ({city.Population:N0})",
    { ValidationErrors: { } errors } => string.Join(
        Environment.NewLine,
        errors.Select(pair => $"{pair.Key}: {string.Join(" ", pair.Value)}").Prepend($"{result.Status}:")),
    _ => $"{result.Status}: {result.ErrorMessage}",
};
