using Hlibz.Results;

namespace Hlibz.UseCases.Generators.Tests;

// A union compiles to a struct without equality, which CA1815 flags.
#pragma warning disable CA1815

/// <summary>The error set of the in-memory handlers (see <see cref="NameTaken"/>).</summary>
public union RenameError(NameTaken);
#pragma warning restore CA1815
