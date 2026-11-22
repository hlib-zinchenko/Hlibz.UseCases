# Validation engine: findings

Which engine should Hlibz.UseCases use to check DataAnnotations on use case records, without
depending on ASP.NET Core? The candidate was **Microsoft.Extensions.Validation** (M.E.Validation),
the source-generated engine behind ASP.NET Core's `AddValidation()`. Probed on SDK 11.0.100 with
package 11.0.0 (net11.0) and 10.0.15 (net10.0), on 2026-11-22.

The probe: a `Requests` class library and a `Module2` class library holding `[ValidatableType]`
records, and a console `Host` referencing both plus `Microsoft.Extensions.DependencyInjection`.
The host calls `AddValidation()` itself, through the library, or both, then validates each
record through `ValidationOptions.TryGetValidatableTypeInfo`.

## TL;DR

The engine is right, but its discovery model makes it a poor fit. Every use case record would
need `[ValidatableType]`, and every assembly holding them its own `AddValidation()` call.
.NET 10 adds an experimental-API error and no `internal` records, and the API differs between
10 and 11. Our generator already finds every use case record, so it can do the validation
itself.

| Question | Answer |
|---|---|
| ASP.NET Core dependency? | **None.** The runtime config names only `Microsoft.NETCore.App`; transitively only `Microsoft.Extensions.Localization.Abstractions`. The generator ships inside the package. |
| What validates? | Nested records, collections (`Costs[0].Value`), attributes on record constructor parameters, custom `ValidationAttribute`s, `IValidatableObject`. |
| How are types found? | Only types marked `[ValidatableType]`, or reachable through a marked type's properties, **in a project that itself calls `AddValidation()`** (otherwise warning ASP0038, and nothing is generated). |
| Host sees another assembly's records? | **No.** The host's `AddValidation()` registers the host's types only. A library's own `AddValidation()` call registers its types, and several calls compose (each adds a resolver). `Module2`, with no call, is never found. |
| Mark once, on `IUseCase` or a base record? | **No.** `[ValidatableType]` is valid on classes only (CS0592 on an interface), and a marked base record doesn't register its derived records. Reachability does: a type used as a property of a marked type is registered. |
| Error keys | Raw property paths: `Name`, `Household.Adults`, `Costs[0].Value`. A whole-object `ValidationResult` with no member names is keyed `""`. That's what Hlibz.Results.AspNetCore wants (it applies the JSON naming policy itself). |
| `IValidatableObject` order | Runs only when every property rule passed: `CreateCityCommand("Big", 2000, ...)` reports only the range error, not the cross-field one. |
| .NET 10 | `[ValidatableType]` is `[Experimental("ASP0029")]`: an **error** on every use in consumer code until they suppress it. `internal` records aren't supported (not found); on .NET 11 they are. |
| API across 10 and 11 | Different: 10 has `IValidatableInfo`, a required `ValidateContext.ValidationContext`, `Dictionary<string, string[]>` errors. 11 has `IValidatableTypeInfo`, `ValidateContext.ServiceProvider`, `IReadOnlyDictionary<string, IReadOnlyList<ValidationError>>` errors. A package targeting both needs two code paths. |

## What using it would cost consumers

1. `[ValidatableType]` on every use case record. Forgetting it means the record is silently
   never validated. Our generator could report it as a diagnostic, but it can't add the
   attribute: generators don't see each other's output, so a `partial` trick doesn't work.
2. An `AddValidation()` call in every assembly that holds use cases (in Lighthouse, every
   module's `Add<Module>ApplicationServices`). Our `AddUseCases()` can't make that call for
   them, for the same reason: the M.E.Validation generator intercepts call sites in the user's
   own source.
3. On .NET 10: `<NoWarn>ASP0029</NoWarn>` in every such project, and public records only.

All three are ceremony for something our generator already knows: every `IUseCase` record is a
root to validate.

## Decision: our own generator, DataAnnotations semantics

Chosen on 2026-11-22. Built as `Internal/ValidationEngine` over the generator's `ValidatableType`
tables; `tests/Hlibz.UseCases.Tests/ParityTests.cs` runs the same models through both engines on
both target frameworks. One difference found there: on .NET 10, Microsoft.Extensions.Validation
passes a nested attribute the root object as `ValidationContext.ObjectInstance`, so `[Compare]` on
a nested object fails; .NET 11 passes the owning object, and so does this package.

Hlibz.UseCases' generator already walks every handler and its use case record for the
manifest, so it also emits each record's validator:

- **Discovery:** every `IUseCase` record, plus every type reachable through its properties and
  collection items (as M.E.Validation does from a marked root). No marker attribute, no
  per-assembly call, `internal` records on both targets, no experimental API.
- **Semantics copied from M.E.Validation**, so they match what ASP.NET Core users already
  know: attributes on properties and record constructor parameters, run through
  `ValidationAttribute.GetValidationResult` with a `ValidationContext` (member and display
  name, the service provider); nested objects and collection items with indexed paths;
  `IValidatableObject` only after every property rule passed; `""` for a whole-object error; a
  depth limit.
- **One runtime API** on net10.0 and net11.0, using only `System.ComponentModel.DataAnnotations`
  (in the BCL). No package dependency at all.
- **Parity tests:** the net11.0 test project runs the same models through M.E.Validation and our
  validators and asserts identical keys and messages, so any drift fails the build.

What we take on: about a generator's worth of code (attribute and member walk, path building,
collections) that .NET already ships, plus keeping parity as M.E.Validation evolves (for
example .NET 11's `LocalizerProvider`, which v1 would leave out).

**Alternative**, if we'd rather not own the engine: use M.E.Validation, accept costs 1-3, and
have our generator report a missing `[ValidatableType]` or a missing `AddValidation()` call as
errors. The pipeline would then carry the 10/11 split behind `#if`.
