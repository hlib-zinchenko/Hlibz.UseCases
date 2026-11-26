// Atlas over HTTP. The use cases (Atlas.Application) hold every rule; this app only binds HTTP to
// them and sends their results. Run it and try Atlas.Api.http.
using Atlas.Application;
using Atlas.Application.Cities;

using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.Mvc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAtlasApplication();

// One error body (ErrorResponse) for every failure: the code, the message, and the error's data
// as its details (an invalid use case's messages, keyed by field).
builder.Services.AddResultErrorResponses();

WebApplication app = builder.Build();

// Each endpoint returns its handler's Result<CityDto, TError> as is: 200 with the city, or the
// error's status (404 CityNotFound, 409 CityNameTaken, 400 ValidationFailed).
RouteGroupBuilder cities = app.MapGroup("api/cities").WithResultResponses();

// [AsParameters] binds the use case from the route: GetCityQuery's [Range] is checked before the
// handler runs.
cities.MapGet(
    "{id:int}",
    ([AsParameters] GetCityQuery query, [FromServices] IGetCityQueryHandler handler, CancellationToken cancellationToken) =>
        handler.HandleAsync(query, cancellationToken));

// The use case is the body; a created city is a 201 with its location.
cities.MapPost(
        string.Empty,
        (CreateCityCommand command, [FromServices] ICreateCityCommandHandler handler, CancellationToken cancellationToken) =>
            handler.HandleAsync(command, cancellationToken))
    .WithCreatedLocation<CityDto>(city => $"/api/cities/{city.Id}");

app.MapGet("audit", ([FromServices] CityStore store) => store.AuditLog);

await app.RunAsync();
