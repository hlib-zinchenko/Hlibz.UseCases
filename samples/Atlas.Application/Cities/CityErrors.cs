namespace Atlas.Application.Cities;

// The failures the handlers declare, each with the data a client needs to act on it. The code is
// what clients branch on; the message is for logs and developers, not for end users.

/// <summary>No city has the id.</summary>
/// <param name="CityId">The id asked for.</param>
[Error("atlas_1001", ResultStatus.NotFound, "The city doesn't exist.")]
public sealed record CityNotFound(int CityId) : Error;

/// <summary>The country already has a city by that name.</summary>
/// <param name="CityId">The id of the city that has it.</param>
[Error("atlas_1002", ResultStatus.Conflict, "The country already has a city by that name.")]
public sealed record CityNameTaken(int CityId) : Error;
