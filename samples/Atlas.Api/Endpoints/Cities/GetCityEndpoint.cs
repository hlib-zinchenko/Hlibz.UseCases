using Atlas.Application.Cities;

using Hlibz.MinimalApi.Endpoints;
using Hlibz.Results;

using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Cities;

/// <summary>
/// Gets one city by its id.
/// </summary>
/// <remarks>
/// The use case is bound with <c>[AsParameters]</c>: its <c>Id</c> comes from the route, with its
/// <c>[Range]</c> in the parameter's schema.
/// </remarks>
internal sealed class GetCityEndpoint : IEndpoint<CitiesEndpointGroup>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapGet("{id:int}", HandleAsync)
            .WithSummary("Gets a city.")
            .ProducesErrors(StatusCodes.Status404NotFound);

    private static Task<Result<CityDto>> HandleAsync(
        [AsParameters] GetCityQuery query,
        [FromServices] IGetCityQueryHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(query, cancellationToken);
}
