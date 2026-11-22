# Samples

One set of use cases, two clients: the point of Hlibz.UseCases.

| Project | What it shows |
|---|---|
| `Atlas.Application` | The use cases: `CreateCityCommand` (DataAnnotations on record parameters, a nested `District` list, a self-describing `[CountryCode]` rule, `[RuleSummary]` for the handler's uniqueness rule), `GetCityQuery`, their handlers, and `AuditFilter`, a filter every use case runs through. No ASP.NET Core. |
| `Atlas.Api` | The HTTP client, on the Hlibz stack: endpoint classes (Hlibz.MinimalApi.Endpoints) that bind the use case (as the body, or `[AsParameters]`) and return its `Result` as is (Hlibz.Results.AspNetCore, `ErrorResponse` for every failure); an OpenAPI document (Hlibz.OpenApi.AspNetCore and Hlibz.OpenApi.UseCases) with the use cases' rules and the validation `400`, written to `openapi/` on every build; ReDoc (Hlibz.Redoc.AspNetCore) at `/api-docs`. Try `Atlas.Api.http`. |
| `Atlas.Console` | A console host standing in for an MCP server or a gRPC service: same use cases, same validation, same audit, no ASP.NET Core in its runtime config. |

```bash
dotnet run --project samples/Atlas.Api                                    # http://localhost:5300/api-docs
dotnet run --project samples/Atlas.Console -- create "" ua 0              # validation failure
dotnet run --project samples/Atlas.Console -- create Lviv UA 717273      # created, audited
```

What to look for in `openapi/Atlas.Api_atlas.json` (or ReDoc):

- `CreateCityCommand.name` has `maxLength: 60` and `population` its `minimum`/`maximum`: rules on
  record parameters, which ASP.NET Core leaves out and Hlibz.OpenApi.UseCases publishes.
- `countryCode` has `pattern` and the sentence `[CountryCode]` describes itself with
  (`IDescribedRule`), listed in `x-rules`; the type's description ends with its `[RuleSummary]`.
- Both operations document a `400` with `ErrorResponse`: each handler's use case can fail
  validation. The `id` route parameter has `minimum: 1` from `GetCityQuery`.
