namespace Atlas.Application.Cities;

/// <summary>A district of a city.</summary>
/// <param name="Name">The district's name.</param>
/// <param name="Population">How many people live there.</param>
public sealed record District([Required] string Name, [Range(0, 10_000_000)] int Population);
