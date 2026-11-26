using Hlibz.Results;

namespace Hlibz.UseCases.Generators.Tests;

/// <summary>
/// An error the in-memory handlers declare. The generator tests compile with Roslyn 4.8, which
/// can't declare a union, so <see cref="RenameError"/> and its errors come from this assembly,
/// compiled by the SDK with the Hlibz.Results generator.
/// </summary>
/// <param name="Name">The name.</param>
[Error("generated_1001", ResultStatus.Conflict, "The name is taken.")]
public sealed record NameTaken(string Name) : Error;
