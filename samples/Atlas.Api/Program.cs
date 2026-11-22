// Atlas on the Hlibz stack. The use cases (Atlas.Application) hold every rule; this app only
// binds HTTP to them, sends their Results, and documents what they check.
// Run it and open http://localhost:5300/api-docs.
using Atlas.Application;
using Atlas.Application.Cities;

using Hlibz.MinimalApi.Endpoints;
using Hlibz.OpenApi;
using Hlibz.Redoc.Extensions;
using Hlibz.Results.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddAtlasApplication();

// One error body (ErrorResponse) for every failure, an invalid use case's included.
builder.Services.AddResultErrorResponses();

// Microsoft.AspNetCore.OpenApi builds the document again on every request; cache it instead.
builder.Services.AddOutputCache();
builder.Services.AddOpenApi("atlas", options => options
    .AddXmlDocumentation()
    .AddOpenApiRules()
    .AddUseCases(useCases => useCases.ValidationResponseType = typeof(ErrorResponse))
    .AddDescriptionCleanup());

WebApplication app = builder.Build();
app.UseOutputCache();
app.MapOpenApi().CacheOutput();

// Every endpoint returns its use case's Result as is; ErrorResponse is the documented error body.
app.MapEndpoints(typeof(Program).Assembly, options => options
    .UseErrorResponse<ErrorResponse>()
    .ConfigureEndpoints(endpoint => endpoint.Builder.WithResultResponses()));

// showExtensions makes each rule's x-rules list visible.
app.MapReDocDocuments(
    [new ReDocDocument("atlas", "Atlas")],
    docs =>
    {
        docs.DefaultColorMode = ReDocColorMode.System;
        docs.AdditionalConfigJson = """{ "showExtensions": ["x-rules"] }""";
    });

app.MapGet("audit", (CityStore store) => store.AuditLog).ExcludeFromDescription();
app.MapGet("/", () => Results.Redirect("/api-docs")).ExcludeFromDescription();

await app.RunAsync();
