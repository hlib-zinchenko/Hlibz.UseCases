![Hlibz.UseCases icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.UseCases/main/docs/icon.png)

# Hlibz.UseCases

[![NuGet](https://img.shields.io/nuget/v/Hlibz.UseCases.svg)](https://www.nuget.org/packages/Hlibz.UseCases)
[![CI](https://github.com/hlib-zinchenko/Hlibz.UseCases/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.UseCases/actions/workflows/ci.yml)

Use cases for .NET apps with more than one client: HTTP endpoints, MCP tools, gRPC services and
jobs all call the same handler, and every rule lives in the use case, validation included,
written as DataAnnotations on the use case record.

```bash
dotnet add package Hlibz.UseCases
```

```csharp
public sealed record CreateCityCommand(
    [Required, MaxLength(60)] string Name,
    [Range(1, 50_000_000)] int Population) : IUseCase;

[Error("atlas_1002", ResultStatus.Conflict, "The country already has a city by that name.")]
public sealed record CityNameTaken(int CityId) : Error;

public union CreateCityError(CityNameTaken);

public interface ICreateCityCommandHandler : IUseCaseHandler<CreateCityCommand, CityDto, CreateCityError>;

internal sealed class CreateCityCommandHandler(AtlasDbContext db) : ICreateCityCommandHandler
{
    public async Task<Result<CityDto, CreateCityError>> HandleAsync(CreateCityCommand useCase, CancellationToken cancellationToken = default)
    {
        ... // only valid use cases get here; return the city, or new CityNameTaken(namesake.Id)
    }
}

builder.Services.AddUseCases([typeof(CreateCityCommand).Assembly]);
builder.Services.AddUseCaseFilter<AuditFilter>(); // optional: runs for every use case

app.MapPost("cities", async (CreateCityCommand command, ICreateCityCommandHandler handler, CancellationToken ct) =>
    (await handler.HandleAsync(command, ct)).ToHttpResult());
```

- **Declared errors:** a handler returns Hlibz.Results' `Result<TResult, TError>`, `TError` a
  `union` of error types, each with its own data. Clients switch over them exhaustively, and
  documentation lists every one. (`IUseCaseHandler<TUseCase, TResult>` and
  `IUseCaseHandler<TUseCase>` remain for errors left undeclared.)
- A **source generator** lists the handlers at compile time (no assembly scan) and wraps each one
  in a pipeline: validation, your `IUseCaseFilter`s, the handler. Clients inject the handler's own
  interface; there is no mediator.
- **Validation without ASP.NET Core** that fails exactly as ASP.NET Core's `AddValidation()` does:
  record parameter attributes, nested objects and collection items (`Costs[0].Value`),
  `IValidatableObject`, async rules. An invalid use case fails with Hlibz.Results' ambient
  `ValidationFailed`, its messages keyed by property path, and never reaches the handler.
- **Rules that describe themselves:** `IDescribedRule` and `[RuleSummary]` say what a custom rule
  checks, shaped like JSON Schema, for an OpenAPI document or a tool schema to publish.
- A handler it can't register is a **build error** (`HUC001`-`HUC007`).

Targets .NET 11 (C# 15 unions). .NET 10 apps stay on 1.x.

## A family of packages

One of five packages I build to set up new .NET APIs the way I want them: Hlibz.Results,
Hlibz.UseCases, Hlibz.MinimalApi.Endpoints, Hlibz.OpenApi and Hlibz.Paging. Each works on its own,
and they work best together, as in [Hlibz.Stack](https://github.com/hlib-zinchenko/Hlibz.Stack). New
minor and major versions come often, a new major whenever .NET gives me a better way to do
something. Breaking changes only ever come in a major and are listed first in its
[release notes](https://github.com/hlib-zinchenko/Hlibz.UseCases/releases). You're welcome to use
the packages, and to [open an issue](https://github.com/hlib-zinchenko/Hlibz.UseCases/issues) when
something is wrong or missing.

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.UseCases](https://github.com/hlib-zinchenko/Hlibz.UseCases).
