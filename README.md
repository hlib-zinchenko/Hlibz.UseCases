![Hlibz.UseCases icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.UseCases/main/docs/icon.png)

# Hlibz.UseCases

[![CI](https://github.com/hlib-zinchenko/Hlibz.UseCases/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.UseCases/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/hlib-zinchenko/Hlibz.UseCases/blob/main/LICENSE)<br>
[![Hlibz.UseCases](https://img.shields.io/nuget/v/Hlibz.UseCases?label=Hlibz.UseCases)](https://www.nuget.org/packages/Hlibz.UseCases)

Use cases for .NET apps with more than one client. HTTP endpoints, MCP tools, gRPC services and
jobs all call the same handler, and every rule lives in the use case: its validation too, written
as DataAnnotations on the use case record itself.

- **Contracts:** a record implementing `IUseCase`, a handler implementing
  `IUseCaseHandler<TUseCase, TResult>` and returning a
  [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results) `Result`.
- **No scanning:** a source generator lists the handlers at compile time and reports one it can't
  register as a build error.
- **A pipeline per handler,** generated: validation, then your filters, then the handler. Clients
  inject the handler's own interface and never see it.
- **Validation without ASP.NET Core,** failing exactly as ASP.NET Core's `AddValidation()` does:
  same order, same messages, same keys (`Costs[0].Value`), checked by parity tests.
- **Rules that describe themselves** (`IDescribedRule`, `[RuleSummary]`), for an OpenAPI document or
  a tool schema to publish.

Targets .NET 10 and 11.

```bash
dotnet add package Hlibz.UseCases
```

```csharp
using System.ComponentModel.DataAnnotations;
using Hlibz.Results;
using Hlibz.UseCases;

public sealed record CreateCityCommand(
    [Required, MaxLength(60)] string Name,
    [Range(1, 50_000_000)] int Population,
    IReadOnlyList<District>? Districts) : IUseCase;

public sealed record District([Required] string Name, [Range(0, 10_000_000)] int Population);

public interface ICreateCityCommandHandler : IUseCaseHandler<CreateCityCommand, CityDto>;

internal sealed class CreateCityCommandHandler(AtlasDbContext db) : ICreateCityCommandHandler
{
    public async Task<Result<CityDto>> HandleAsync(CreateCityCommand useCase, CancellationToken cancellationToken = default)
    {
        // Only valid use cases get here.
        ...
    }
}
```

```csharp
builder.Services.AddUseCases([typeof(CreateCityCommand).Assembly]);

// An HTTP endpoint...
app.MapPost("cities", async (CreateCityCommand command, ICreateCityCommandHandler handler, CancellationToken ct) =>
    (await handler.HandleAsync(command, ct)).ToHttpResult());

// ...and an MCP tool, the same use case. (ToCallToolResult is your MCP server's own helper that
// turns a Result into a tool result, written once, as ToHttpResult is for HTTP.)
[McpServerTool(Name = "create_city")]
public static async Task<CallToolResult> CreateCityAsync(ICreateCityCommandHandler handler, string name, int population, CancellationToken ct) =>
    (await handler.HandleAsync(new CreateCityCommand(name, population, Districts: null), ct)).ToCallToolResult();
```

An invalid use case never reaches the handler:

```csharp
Result<CityDto> result = await handler.HandleAsync(new CreateCityCommand("", 0, [new District("Podil", -1)]));
// result.Status           Invalid
// result.ValidationErrors { "Name": [...], "Population": [...], "Districts[0].Population": [...] }
```

## Use cases and handlers

A use case is a record implementing `IUseCase`: ids and plain values, never a data model. Its
handler implements `IUseCaseHandler<TUseCase, TResult>` (or `IUseCaseHandler<TUseCase>` for one
that returns no value) and returns a `Result`: expected failures are results, never exceptions.
Give each handler an empty interface of its own and inject that in clients:

```csharp
public interface IDeleteCityCommandHandler : IUseCaseHandler<DeleteCityCommand>;
```

`services.AddUseCases(assemblies)` registers every handler of those assemblies, **scoped**, wrapped
in its pipeline, under its own interface(s), the closed `IUseCaseHandler<...>` and the marker
`IUseCaseHandler`. The handler class itself is registered too (`TryAdd`), so its own
dependencies resolve as usual. Call it once per module if you like: each call adds assemblies,
and an assembly already added is skipped. Two handlers for one use case is a build error in one
assembly and an exception across assemblies.

A test that resolves every handler catches one whose dependencies are missing:

```csharp
using IServiceScope scope = provider.CreateScope();
_ = scope.ServiceProvider.GetServices<IUseCaseHandler>().ToList();
```

`IUseCaseCatalog` (a singleton) lists the registered use cases: type, handler, result type and
whether it can fail validation, for adapters that describe them (an OpenAPI document, MCP tool
schemas).

## Validation

Before the handler runs, the pipeline validates the use case against its DataAnnotations, the
way ASP.NET Core's validation (Microsoft.Extensions.Validation) does, with none of ASP.NET Core:

- Attributes on properties and on **record constructor parameters**, custom `ValidationAttribute`s
  included. `[Required]` runs first; when it fails, nothing else on that property does.
- **Nested** objects and **collection items**, each validated as its runtime type:
  `Household.Adults`, `Costs[0].Value`.
- Then, only when every property passed, the **type's own attributes**, and only when those passed
  too, **`IValidatableObject`**.
- On .NET 11, `AsyncValidationAttribute`s (after the synchronous ones pass) and
  `IAsyncValidatableObject`.
- `[Display(Name = "...")]` names the field in messages; without it, `DisplayNamePolicy` (see
  options) can name it as your clients do: `The first_name field is required.`

An invalid use case returns `Result.Invalid(errors)` (or `Result.Error(errors)`, see options)
keyed by property path. [Hlibz.Results.AspNetCore](https://www.nuget.org/packages/Hlibz.Results.AspNetCore)
sends it as `400` with the keys in your JSON naming (`districts[0].population`).

There is nothing to mark and nothing to call per assembly: every type a use case reaches through
its public properties is validated, and the generator writes the tables the validation walks (no
reflection to read values; attributes are read once each and cached).

```csharp
builder.Services.AddUseCases([typeof(CreateCityCommand).Assembly], options =>
{
    options.Validation.FailureStatus = ResultStatus.Error; // Invalid by default
    options.Validation.FailureMessage = "Check the fields.";
    options.Validation.MaxDepth = 32;                      // a circular reference throws past it
    options.Validation.DisplayNamePolicy = JsonNamingPolicy.SnakeCaseLower; // names in messages
    options.Validation.Enabled = true;
});
```

`IUseCaseValidator` (scoped) validates a use case without handling it, for a client that wants to
check input early.

**Where it differs from Microsoft.Extensions.Validation:**

- No localization: messages are the attributes' own (`ErrorMessageResourceType` works as usual).
- `DisplayNamePolicy` is this package's own: Microsoft's engine always names a field without
  `[Display]` as declared, which is also the default here. Keys are property paths either way.
- Asynchronous attributes run one at a time, stopping at the first failure, rather than
  concurrently.
- On .NET 10, ASP.NET Core's engine passes a nested attribute the *root* object as
  `ValidationContext.ObjectInstance`, so `[Compare]` on a nested object fails there. This package
  passes the object the property belongs to, as .NET 11 does.
- An `IValidatableObject` result naming no member is keyed `""` wherever the object sits, as there.

## Filters

A filter runs for every use case, after validation and before its handler, in registration order:

```csharp
builder.Services.AddUseCaseFilter<AuditFilter>();

public sealed class AuditFilter(IAuditLog log) : IUseCaseFilter
{
    public async ValueTask<Result> InvokeAsync(UseCaseContext context, UseCaseFilterDelegate next)
    {
        Result result = await next(context);
        log.Write(context.UseCaseType.Name, result.Status);
        return result;
    }
}
```

A filter sees the plain `Result`. It continues with `next`, or stops with a failure of its own
(`Result.Forbidden()`), which the pipeline turns into the handler's `Result<T>`. A success without
calling `next` throws: there would be no value. `UseCaseContext` carries the use case, its declared
type, the handler type, the scope's services and the cancellation token. Authorization by an
attribute on the use case, logging and transactions are typical filters.

## Rules that describe themselves

DataAnnotations' own attributes are understood by OpenAPI generators already; a custom one isn't.
Implement `IDescribedRule` and it says what it checks, shaped like JSON Schema:

```csharp
public sealed class CountryCodeAttribute : ValidationAttribute, IDescribedRule
{
    public override bool IsValid(object? value) => ...;

    public void Describe(RuleDescription rule)
    {
        rule.Pattern = "^[A-Z]{2}$";                 // a keyword, so generated clients enforce it
        rule.AddText("A two-letter ISO country code."); // a sentence, for people
    }
}
```

`RuleDescription` has `Pattern`, `MinLength`/`MaxLength`, `Minimum`/`Maximum`,
`ExclusiveMinimum`/`ExclusiveMaximum`, `MinItems`/`MaxItems`, `UniqueItems`, `NotConst` and a
`Keywords` object for anything else. A rule with no attribute of its own (a cross-field check in
`IValidatableObject`, or one only the handler can check) takes `[RuleSummary("...")]` on the type,
property or parameter. Nothing here depends on OpenAPI: an adapter
([Hlibz.OpenApi.UseCases](https://www.nuget.org/packages/Hlibz.OpenApi.UseCases) for
Microsoft.AspNetCore.OpenApi) publishes it.

## OpenAPI

[Hlibz.OpenApi.UseCases](https://www.nuget.org/packages/Hlibz.OpenApi.UseCases), from the
[Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi#use-cases) repository, puts what
use cases check into documents built with Microsoft.AspNetCore.OpenApi:

```bash
dotnet add package Hlibz.OpenApi.UseCases --prerelease
```

```csharp
builder.Services.AddOpenApi("atlas", options => options
    .AddXmlDocumentation()                       // Hlibz.OpenApi.AspNetCore
    .AddUseCases(useCases => useCases.ValidationResponseType = typeof(ErrorResponse)));
```

- Every `IDescribedRule` and `[RuleSummary]` becomes schema keywords plus a sentence in
  `description` and `x-rules`, on the property, record parameter, parameter or type it sits on
  (next to the `$ref` for a component, as Hlibz.OpenApi's own rules are).
- DataAnnotations on **positional record parameters** (`[Range]`, `[MinLength]`, `[MaxLength]`,
  `[StringLength]`, `[Length]`, `[RegularExpression]`, `[Url]`, `[Base64String]`) are written as
  ASP.NET Core writes them on properties. It only reads properties, so a use case written as a
  positional record would otherwise publish none of them.
- Every operation whose handler takes the handler of a use case that can fail validation gets a
  `400` (`HttpValidationProblemDetails` unless you say otherwise), unless it already documents one.

Rules reach the document only on types that are part of the HTTP contract: bind the use case
itself (as the body, or with `[AsParameters]`), or give it a record that is also the body
(`UpdateCityCommand(int Id, CityChanges Changes)`). `samples/Atlas.Api` shows the whole stack.

## Build errors

| Id | Severity | When |
|---|---|---|
| HUC001 | Error | A handler doesn't implement exactly one `IUseCaseHandler<...>`. |
| HUC002 | Error | A handler, its use case, result or interface isn't accessible to the generated code (a `private` nested type). |
| HUC003 | Warning | A generic handler is never registered; make it `abstract` if it is a base. |
| HUC004 | Error | A handler's own interface declares members; the generated pipeline only implements `HandleAsync`. |
| HUC005 | Error | Two handlers in one assembly handle the same use case. |
| HUC006 | Error | A handler is a struct. |
| HUC007 | Error | A handler has no public constructor for the container. |

## Performance and AOT

- No assembly scan at startup: `AddUseCases` reads the list the generator wrote.
- No mediator or dispatcher: the client calls the generated pipeline, which calls the handler. A
  use case without filters and with validation off is one extra call.
- The package is marked AOT-compatible. Validation reads property values through generated
  delegates and attributes through reflection on the types the generator names (kept by trimming).

## Sample

[`samples`](samples/README.md): one `Atlas.Application` library with the use cases, an API on the
Hlibz stack (`Atlas.Api`: Hlibz.MinimalApi.Endpoints, Hlibz.Results.AspNetCore,
Hlibz.OpenApi.AspNetCore with Hlibz.OpenApi.UseCases, Hlibz.Redoc.AspNetCore) and a console host
(`Atlas.Console`, no ASP.NET Core) running them.

## Related packages

- [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results): the `Result` types handlers return,
  and [Hlibz.Results.AspNetCore](https://www.nuget.org/packages/Hlibz.Results.AspNetCore) to send
  them as HTTP responses.
- [Hlibz.OpenApi.UseCases](https://www.nuget.org/packages/Hlibz.OpenApi.UseCases): use cases in
  OpenAPI documents, on the
  [Hlibz.OpenApi.AspNetCore](https://www.nuget.org/packages/Hlibz.OpenApi.AspNetCore) transformers.
- [Hlibz.Redoc.AspNetCore](https://www.nuget.org/packages/Hlibz.Redoc.AspNetCore): ReDoc for those
  documents, served from the app.
- [Hlibz.MinimalApi.Endpoints](https://www.nuget.org/packages/Hlibz.MinimalApi.Endpoints): minimal
  API endpoints as classes, a natural HTTP client for use cases.

## Versioning

Every package in this repository is released under one version, from one `vX.Y.Z` tag.

Release notes: [GitHub Releases](https://github.com/hlib-zinchenko/Hlibz.UseCases/releases).

## License

MIT
