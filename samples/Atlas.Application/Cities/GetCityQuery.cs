namespace Atlas.Application.Cities;

/// <summary>Gets one city.</summary>
/// <param name="Id">The city's id.</param>
public sealed record GetCityQuery([Range(1, int.MaxValue)] int Id) : IUseCase;
