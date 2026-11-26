namespace Atlas.Application.Cities;

// Each use case's errors, as a union: its handler returns Result<CityDto, TError>, and clients
// switch over the cases. A union compiles to a struct without equality, which CA1815 flags.
#pragma warning disable CA1815

public union GetCityError(CityNotFound);

public union CreateCityError(CityNameTaken);
