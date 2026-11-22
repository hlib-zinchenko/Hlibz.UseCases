using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hlibz.UseCases.Generators.Tests;

/// <summary>
/// One source compiled in memory with the generator: what it reported, what it wrote, and the
/// assembly it all compiles to.
/// </summary>
internal sealed class GeneratorRun
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.ComponentModel.DataAnnotations;
        using System.Threading;
        using System.Threading.Tasks;
        using Hlibz.Results;
        using Hlibz.UseCases;

        """;

    private static readonly MetadataReference[] AllReferences =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    private GeneratorRun(ImmutableArray<Diagnostic> generatorDiagnostics, Compilation output, string? source)
    {
        GeneratorDiagnostics = generatorDiagnostics;
        Output = output;
        Source = source;
    }

    /// <summary>
    /// Gets what the generator reported.
    /// </summary>
    public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

    /// <summary>
    /// Gets the compilation with the generated source added.
    /// </summary>
    public Compilation Output { get; }

    /// <summary>
    /// Gets the generated source, or null when nothing was generated.
    /// </summary>
    public string? Source { get; }

    /// <summary>
    /// Gets the ids of what the generator reported.
    /// </summary>
    public string[] DiagnosticIds => [.. GeneratorDiagnostics.Select(item => item.Id)];

    public static GeneratorRun Of(string source, bool referenceLibrary = true)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.CSharp12);
        MetadataReference[] references = referenceLibrary
            ? AllReferences
            : [.. AllReferences.Where(reference => !reference.Display!.EndsWith("Hlibz.UseCases.dll", StringComparison.Ordinal))];
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Generated.UseCases-1",
            [CSharpSyntaxTree.ParseText(Usings + source, parseOptions, path: "Source0.cs")],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new UseCaseManifestGenerator())
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out Compilation output,
                out ImmutableArray<Diagnostic> diagnostics);

        string? generated = driver.GetRunResult().GeneratedTrees
            .Select(tree => tree.ToString())
            .SingleOrDefault();
        return new GeneratorRun(diagnostics, output, generated);
    }

    /// <summary>
    /// Emits the output and loads it into a context of its own, failing on any compile error or
    /// warning (the generated source's included).
    /// </summary>
    /// <returns>The loaded assembly.</returns>
    public Assembly Load()
    {
        Assert.Empty(CompileProblems());
        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = Output.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        stream.Position = 0;
        return new AssemblyLoadContext(name: null, isCollectible: true).LoadFromStream(stream);
    }

    /// <summary>
    /// Gets the output's compile errors and warnings (the generated source's included).
    /// </summary>
    /// <returns>The problems.</returns>
    public Diagnostic[] CompileProblems() =>
    [
        .. Output.GetDiagnostics().Where(diagnostic =>
            diagnostic.Severity >= DiagnosticSeverity.Warning),
    ];
}
