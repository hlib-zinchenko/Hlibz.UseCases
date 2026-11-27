# Samples

One set of use cases, two clients: the point of Hlibz.UseCases.

| Project | What it shows |
|---|---|
| `Atlas.Application` | The use cases: `CreateCityCommand` (DataAnnotations on record parameters, a nested `District` list, a self-describing `[CountryCode]` rule, `[RuleSummary]` for the handler's uniqueness rule), `GetCityQuery`, and handlers that declare their errors (`Result<CityDto, CreateCityError>`: the errors in `Cities/CityErrors.cs`, their unions in `Cities/CityErrorSets.cs`). `AuditFilter` is a filter every use case runs through. No ASP.NET Core. |
| `Atlas.Api` | The HTTP client: plain minimal API endpoints that bind the use case (as the body, or `[AsParameters]`) and return its result as is; Hlibz.Results.AspNetCore sends it (`ErrorResponse` for every failure, each error's data in `details`). Port 5300; try `Atlas.Api.http`. |
| `Atlas.Console` | A console host standing in for an MCP server or a gRPC service: same use cases, same validation, same audit, no ASP.NET Core in its runtime config. It switches over each handler's declared errors. |

```bash
dotnet run --project samples/Atlas.Api                                    # http://localhost:5300, try Atlas.Api.http
dotnet run --project samples/Atlas.Console -- create "" ua 0              # validation failure
dotnet run --project samples/Atlas.Console -- create Kyiv UA 1            # CityNameTaken, with the city's id
dotnet run --project samples/Atlas.Console -- create Lviv UA 717273      # created, audited
```

The samples use only the Hlibz packages they need (Hlibz.Results.AspNetCore over HTTP). The OpenAPI
document with the use cases' rules and each declared error (Hlibz.OpenApi.UseCases,
Hlibz.OpenApi.Results) is in Hlibz.Stack, where the packages meet.
