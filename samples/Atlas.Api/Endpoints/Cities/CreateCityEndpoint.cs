using Atlas.Application.Cities;

using Hlibz.MinimalApi.Endpoints;
using Hlibz.Results;

using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Endpoints.Cities;

/// <summary>
/// Adds a city to a country and returns it, with its new id.
/// </summary>
/// <remarks>
/// The use case is the body: its DataAnnotations, its <c>[CountryCode]</c> rule and its
/// <c>[RuleSummary]</c> are all in the document, and the <c>400</c> comes from
/// Hlibz.OpenApi.UseCases, because the handler's use case can fail validation.
/// </remarks>
internal sealed class CreateCityEndpoint : IEndpoint<CitiesEndpointGroup>
{
    public RouteHandlerBuilder Map(IEndpointRouteBuilder group) =>
        group.MapPost(string.Empty, HandleAsync)
            .WithSummary("Adds a city.")
            .ProducesErrors(StatusCodes.Status409Conflict);

    private static Task<Result<CityDto>> HandleAsync(
        CreateCityCommand command,
        [FromServices] ICreateCityCommandHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(command, cancellationToken);
}
