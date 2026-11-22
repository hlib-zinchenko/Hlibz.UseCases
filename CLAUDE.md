# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

One NuGet package, `src/Hlibz.UseCases`, with its source generator `src/Hlibz.UseCases.Generators`
packed inside it (`analyzers/dotnet/cs`, plus `buildTransitive/Hlibz.UseCases.targets` that adds it
to an indirect consumer if the SDK hasn't). Use cases (`IUseCase`), handlers
(`IUseCaseHandler<TUseCase[, TResult]>` returning Hlibz.Results' `Result`), a generated pipeline
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
  through `IUseCaseValidator` → `Internal/ValidationEngine`, then runs the filters, then the
  handler; a filter's failure becomes the handler's `Result<T>` through `FromFailed`.
- `Internal/ValidationEngine` is a port of Microsoft.Extensions.Validation's rules (see its
  remarks): keep it in step, and keep `tests/Hlibz.UseCases.Tests/ParityTests.cs` green, which
  runs the same models through both engines. `Internal/RuleAttributes` reads property and record
  parameter attributes once, as that engine's `ValidationAttributeCache` does.
- Async validation (`AsyncValidationAttribute`, `IAsyncValidatableObject`) exists only on .NET 11:
  `#if NET11_0_OR_GREATER`.
- The text-only rule attribute is `[RuleSummary]`, not `[RuleText]`: that name would clash with
  Hlibz.OpenApi's `RuleText` class wherever both namespaces are in scope.

## Requires

* The .NET SDK `global.json` pins (11.0.100, patches roll forward), plus the .NET 10 runtime to run
  both test targets. Pinned because warnings are errors and `AnalysisMode=All`.

## Commands

```bash
dotnet build
dotnet test
dotnet test -f net11.0
dotnet pack -c Release -o ./artifacts
.github/scripts/check-packed-generator.sh ./artifacts   # an indirect consumer of the nupkg, net10.0
```

## Conventions

StyleCop + Sonar + `AnalysisMode=All`, warnings as errors; every public member has XML docs.
Explicit types, no `var`. The generator targets netstandard2.0 on Roslyn 4.8 (a low minimum: every
SDK that builds net10.0 loads it). Generated code is fully qualified (`global::`), names types for
`typeof` with `SymbolNames.TypeOf` and as type arguments with `SymbolNames.Argument` (nullable
annotations and tuple names kept, so the pipeline matches the interface exactly). Tests: xUnit v3
on Microsoft Testing Platform, named `Subject_WithCondition_ExpectedOutcome`; generator tests
compile sources in memory (`GeneratorRun`) and fail on any compile warning in generated code.

Targets net10.0 and net11.0. Versions are central (`Directory.Packages.props`); the
Microsoft.Extensions abstractions and Microsoft.CodeAnalysis.CSharp are minimums. Shared NuGet
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
