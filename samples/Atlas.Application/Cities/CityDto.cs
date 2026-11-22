namespace Atlas.Application.Cities;

/// <summary>A city.</summary>
/// <param name="Id">The city's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="CountryCode">Its country's ISO code.</param>
/// <param name="Population">How many people live there.</param>
public sealed record CityDto(int Id, string Name, string CountryCode, int Population);
