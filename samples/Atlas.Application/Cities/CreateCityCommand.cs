namespace Atlas.Application.Cities;

/// <summary>Adds a city to a country.</summary>
/// <param name="Name">The city's name, as it's written locally.</param>
/// <param name="CountryCode">The country's ISO code.</param>
/// <param name="Population">How many people live there.</param>
/// <param name="Districts">Its districts, if any.</param>
[RuleSummary("Name must be unique within the country.")]
public sealed record CreateCityCommand(
    [Required, MaxLength(60)] string Name,
    [Required, CountryCode] string CountryCode,
    [Range(1, 50_000_000)] int Population,
    IReadOnlyList<District>? Districts) : IUseCase;
