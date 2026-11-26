# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

One NuGet package, `src/Hlibz.UseCases`, with its source generator `src/Hlibz.UseCases.Generators`
packed inside it (`analyzers/dotnet/cs`, plus `buildTransitive/Hlibz.UseCases.targets` that adds it
to an indirect consumer if the SDK hasn't). Use cases (`IUseCase`), handlers
(`IUseCaseHandler<TUseCase, TResult, TError>` returning Hlibz.Results 3.0's `Result<TResult, TError>`,
`TError` a C# 15 `union` of error types; `IUseCaseHandler<TUseCase[, TResult]>` returning
`Result`/`Result<TResult>` for undeclared errors), a generated pipeline
per handler (validation, `IUseCaseFilter`s, the handler) and self-describing rules
(`IDescribedRule`, `RuleDescription`, `[RuleSummary]`). No ASP.NET Core dependency anywhere in `src`.
`docs/VALIDATION_ENGINE_FINDINGS.md` records why validation is generated here rather than taken
from Microsoft.Extensions.Validation.

How it fits together:

- The generator (`UseCaseManifestGenerator`, same shape as Hlibz.MinimalApi.Endpoints') finds every
  concrete handler (`HandlerModelBuilder`, diagnostics `HUC001`-`HUC007` in `Diagnostics`), walks
  the types each use case reaches through public properties and collection items
  (`ValidationGraph`), and writes one file (`ManifestWriter`): an assembly-level
  `UseCaseManifestAttribute` subclass listing `UseCaseRegistration`s (with a `Register` lambda that
  adds the handler, `TryAddScoped`, and a `file sealed class UseCasePipelineN` under the closed
  `IUseCaseHandler<...>`, the handler's own interfaces and the marker) and the `ValidatableType`
  tables (typed getter delegates, declaring types for attribute lookup) of the types that can fail.
- `AddUseCases` (`UseCaseServiceCollectionExtensions`) reads the manifests into one
  `UseCaseRegistry` per service collection (also `IUseCaseCatalog`), found among the descriptors so
  repeated calls share it.
- `UseCasePipeline` (scoped, public but `EditorBrowsable.Never`: generated code calls it) validates
  through `IUseCaseValidator` → `Internal/ValidationEngine` (an invalid use case fails with the
  ambient `ValidationFailed`), then runs the filters, then the handler; a filter's own failure
  becomes the handler's result type through its `Failure`, and must be an `AmbientError` for a
  handler declaring its errors (anything else throws `InvalidOperationException`).
- `UseCaseDescriptor.ErrorSetType` (from `UseCaseRegistration`, written by the generator) is the
  handler's `TError`, for adapters; `ErrorSetDescriptor.For` (Hlibz.Results) lists its errors.
- `Internal/ValidationEngine` is a port of Microsoft.Extensions.Validation's rules (see its
  remarks): keep it in step, and keep `tests/Hlibz.UseCases.Tests/ParityTests.cs` green, which
  runs the same models through both engines. `Internal/RuleAttributes` reads property and record
  parameter attributes once, as that engine's `ValidationAttributeCache` does.

- The OpenAPI adapter, `Hlibz.OpenApi.UseCases`, lives in the
  [Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi) repository (a proof of concept
  here first). It reads `IDescribedRule`, `RuleDescription` and `IUseCaseCatalog`, so a change to
  those is a change to it. The attribute is `[RuleSummary]`, not `[RuleText]`: that name would
  clash with Hlibz.OpenApi's `RuleText` class wherever both namespaces are in scope.

- `samples/`: `Atlas.Application` (use cases, handlers declaring their errors in
  `Cities/CityErrors.cs` and `Cities/CityErrorSets.cs`, `AuditFilter`), `Atlas.Api` (plain minimal
  API endpoints and Hlibz.Results.AspNetCore, nothing else from Hlibz; port 5300,
  `Atlas.Api.http`) and `Atlas.Console` (no ASP.NET Core; CI checks its runtime config). Never
  packed. Keep `samples/README.md` in step. No OpenAPI here: Hlibz.Stack is where the packages
  meet.

Until the .NET 11 majors ship, `NuGet.config` adds the local folder feed `~/LocalNuGet`, where
Hlibz.Results 3.0 previews are packed (`check-packed-generator.sh` adds it too, when it exists).

## Requires

* The .NET SDK `global.json` pins (11.0.100, patches roll forward). Pinned because warnings are
  errors and `AnalysisMode=All`.

## Commands

```bash
dotnet build
dotnet test
dotnet pack -c Release -o ./artifacts
.github/scripts/check-packed-generator.sh ./artifacts   # an indirect consumer of the nupkg
dotnet run --project samples/Atlas.Console -- create Lviv UA 717273
dotnet run --project samples/Atlas.Api                  # :5300, try Atlas.Api.http
```

If you start `Atlas.Api`, stop it when you're done.

## Conventions

StyleCop + Sonar + `AnalysisMode=All`, warnings as errors; every public member has XML docs.
Explicit types, no `var`. The generator targets netstandard2.0 on Roslyn 4.8 (a low minimum: every
SDK loads it), with its own pinned `LangVersion`. Generated code is fully qualified (`global::`), names types for
`typeof` with `SymbolNames.TypeOf` and as type arguments with `SymbolNames.Argument` (nullable
annotations and tuple names kept, so the pipeline matches the interface exactly). Tests: xUnit v3
on Microsoft Testing Platform, named `Subject_WithCondition_ExpectedOutcome`; generator tests
compile sources in memory (`GeneratorRun`) and fail on any compile warning in generated code.
Roslyn 4.8 can't parse a `union`, so in-memory sources use error sets declared in the test
assembly itself (`RenameError`), compiled by the SDK.

Targets net11.0 only, from 2.0 (Hlibz.Results 3.0's C# 15 unions); .NET 10 apps stay on 1.x.
The Microsoft.Extensions abstractions it uses are in .NET 11's shared framework, so the package
references none (NU1510). Versions are central (`Directory.Packages.props`); Hlibz.Results and
Microsoft.CodeAnalysis.CSharp are minimums. Shared NuGet
metadata and `Version` live in `Directory.Build.props`. The package has its own
`src/Hlibz.UseCases/README.md` (packed, shown on nuget.org, absolute links only); the root
`README.md` is the full documentation; keep them in step when an API changes. New diagnostics go in
`AnalyzerReleases.Unshipped.md` and the README's build errors table.

## Releasing

1. Set `Version` in `Directory.Build.props`, commit, then push the tag `vX.Y.Z`.
2. The user approves the `release` environment run.
3. Once the run succeeds and
   `https://api.nuget.org/v3-flatcontainer/hlibz.usecases/index.json` lists the version, Claude
   creates the GitHub release that `PackageReleaseNotes` links to:
   `gh release create vX.Y.Z --verify-tag --title X.Y.Z [--prerelease when the version has a suffix] --notes-file <notes>`.
   Claude writes the notes from `git log <previous tag>..vX.Y.Z`: breaking changes first, then
   features and fixes, then the nuget.org link. Nothing is attached.
