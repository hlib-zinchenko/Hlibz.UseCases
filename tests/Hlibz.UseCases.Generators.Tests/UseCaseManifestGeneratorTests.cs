using System.Reflection;

using Hlibz.Results;

using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.UseCases.Generators.Tests;

public sealed class UseCaseManifestGeneratorTests
{
    private const string CreateCity = """
        namespace App;

        public sealed record CreateCityCommand([Required, MaxLength(5)] string Name, IReadOnlyList<Cost> Costs) : IUseCase;

        public sealed record Cost([Range(0, 10)] int Value);

        public sealed record CityDto(string Name);

        public interface ICreateCityCommandHandler : IUseCaseHandler<CreateCityCommand, CityDto>;

        internal sealed class CreateCityCommandHandler : ICreateCityCommandHandler
        {
            public Task<Result<CityDto>> HandleAsync(CreateCityCommand useCase, CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<CityDto>.Success(new CityDto(useCase.Name)));
        }

        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Generate_WithHandler_RegistersItWrappedInValidatingPipeline()
    {
        GeneratorRun run = GeneratorRun.Of(CreateCity);

        Assert.Empty(run.GeneratorDiagnostics);
        Assembly assembly = run.Load();
        ServiceCollection services = new();
        services.AddUseCases([assembly]);
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        Type commandType = assembly.GetType("App.CreateCityCommand")!;
        Type costType = assembly.GetType("App.Cost")!;
        Array costs = Array.CreateInstance(costType, 1);
        costs.SetValue(Activator.CreateInstance(costType, 11), 0);
        object command = Activator.CreateInstance(commandType, "Kharkiv", costs)!;
        object handler = scope.ServiceProvider.GetRequiredService(assembly.GetType("App.ICreateCityCommandHandler")!);
        Task task = (Task)handler.GetType().GetMethod("HandleAsync")!.Invoke(handler, [command, Token])!;
        await task;
        Result result = (Result)task.GetType().GetProperty("Result")!.GetValue(task)!;

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(["Name", "Costs[0].Value"], result.ValidationErrors!.Keys);
    }

    [Fact]
    public void Generate_WithHandler_DescribesItInCatalog()
    {
        Assembly assembly = GeneratorRun.Of(CreateCity).Load();
        ServiceCollection services = new();
        services.AddUseCases([assembly]);
        using ServiceProvider provider = services.BuildServiceProvider();

        UseCaseDescriptor descriptor = Assert.Single(provider.GetRequiredService<IUseCaseCatalog>().UseCases);

        Assert.Equal("App.CreateCityCommand", descriptor.UseCaseType.FullName);
        Assert.Equal("App.CreateCityCommandHandler", descriptor.HandlerType.FullName);
        Assert.Equal("App.CityDto", descriptor.ResultType!.FullName);
        Assert.True(descriptor.CanFailValidation);
    }

    [Fact]
    public void Generate_WithAwkwardShapes_CompilesWithoutWarnings()
    {
        GeneratorRun run = GeneratorRun.Of("""
            namespace App;

            public record BaseCommand([Required] string? Id);

            public sealed record ShapesCommand(
                string? Id,
                [Range(1, 2)] int @class,
                Page<Item> Page,
                Item[] Items,
                Item? Single,
                Pair? Pair) : BaseCommand(Id), IUseCase;

            public sealed record Page<T>(IReadOnlyList<T> Rows);

            public sealed record Item([MinLength(1)] string Name);

            public readonly record struct Pair([Range(0, 1)] int Left, int Right);

            public sealed class Node
            {
                [Range(0, 1)]
                public int Value { get; set; }

                public Node? Next { get; set; }
            }

            public sealed record CycleCommand(Node Root) : IUseCase;

            public readonly record struct StructCommand([Required] string? Name) : IUseCase;

            public interface IShapes : IUseCaseHandler<ShapesCommand, (int Count, string? Name)>;

            public interface IShapesAgain : IShapes;

            internal sealed class ShapesHandler : IShapesAgain
            {
                public Task<Result<(int Count, string? Name)>> HandleAsync(ShapesCommand useCase, CancellationToken cancellationToken = default) =>
                    Task.FromResult(Result<(int Count, string? Name)>.Success((1, null)));
            }

            internal sealed class CycleHandler : IUseCaseHandler<CycleCommand, string?>
            {
                public Task<Result<string?>> HandleAsync(CycleCommand useCase, CancellationToken cancellationToken = default) =>
                    Task.FromResult(Result<string?>.Success(null));
            }

            internal sealed class StructHandler : IUseCaseHandler<StructCommand>
            {
                public Task<Result> HandleAsync(StructCommand useCase, CancellationToken cancellationToken = default) =>
                    Task.FromResult(Result.Success());
            }

            internal abstract class BaseHandler<T> : IUseCaseHandler<StructCommand>
            {
                public abstract Task<Result> HandleAsync(StructCommand useCase, CancellationToken cancellationToken = default);
            }
            """);

        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Empty(run.CompileProblems());
        Assembly assembly = run.Load();
        ServiceCollection services = new();
        services.AddUseCases([assembly]);
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        object shapes = scope.ServiceProvider.GetRequiredService(assembly.GetType("App.IShapesAgain")!);
        Assert.Same(shapes, scope.ServiceProvider.GetRequiredService(assembly.GetType("App.IShapes")!));
        Assert.Equal(3, scope.ServiceProvider.GetServices<IUseCaseHandler>().Count());
    }

    [Fact]
    public void Generate_WithoutLibraryReference_GeneratesNothing()
    {
        GeneratorRun run = GeneratorRun.Of("namespace App; public sealed class Plain;", referenceLibrary: false);

        Assert.Null(run.Source);
    }

    [Fact]
    public void Generate_WithoutHandlers_WritesEmptyManifest()
    {
        Assembly assembly = GeneratorRun.Of("namespace App; public sealed record Plain;").Load();
        ServiceCollection services = new();

        services.AddUseCases([assembly]);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Empty(provider.GetRequiredService<IUseCaseCatalog>().UseCases);
    }

    [Theory]
    [InlineData(
        "HUC001",
        "internal sealed class Both : IUseCaseHandler<A>, IUseCaseHandler<B> { public Task<Result> HandleAsync(A useCase, CancellationToken cancellationToken = default) => null!; public Task<Result> HandleAsync(B useCase, CancellationToken cancellationToken = default) => null!; }")]
    [InlineData(
        "HUC001",
        "internal sealed class Marker : IUseCaseHandler;")]
    [InlineData(
        "HUC002",
        "internal static class Outer { private sealed class Hidden : IUseCaseHandler<A> { public Task<Result> HandleAsync(A useCase, CancellationToken cancellationToken = default) => null!; } }")]
    [InlineData(
        "HUC003",
        "internal sealed class Generic<T> : IUseCaseHandler<A> { public Task<Result> HandleAsync(A useCase, CancellationToken cancellationToken = default) => null!; }")]
    [InlineData(
        "HUC004",
        "public interface IAHandler : IUseCaseHandler<A> { void Extra(); } internal sealed class AHandler : IAHandler { public Task<Result> HandleAsync(A useCase, CancellationToken cancellationToken = default) => null!; public void Extra() { } }")]
    [InlineData(
        "HUC005",
        "internal sealed class One : IUseCaseHandler<A> { public Task<Result> HandleAsync(A useCase, CancellationToken cancellationToken = default) => null!; } internal sealed class Two : IUseCaseHandler<A> { public Task<Result> HandleAsync(A useCase, CancellationToken cancellationToken = default) => null!; }")]
    [InlineData(
        "HUC006",
        "internal struct Valued : IUseCaseHandler<A> { public Task<Result> HandleAsync(A useCase, CancellationToken cancellationToken = default) => null!; }")]
    [InlineData(
        "HUC007",
        "internal sealed class Closed : IUseCaseHandler<A> { private Closed() { } public Task<Result> HandleAsync(A useCase, CancellationToken cancellationToken = default) => null!; }")]
    public void Generate_WithUnusableHandler_ReportsIt(string id, string handler)
    {
        GeneratorRun run = GeneratorRun.Of($$"""
            namespace App;

            public sealed record A : IUseCase;

            public sealed record B : IUseCase;

            {{handler}}
            """);

        Assert.Contains(id, run.DiagnosticIds);
        Assert.DoesNotContain("new global::Hlibz.UseCases.UseCaseRegistration(", run.Source ?? string.Empty, StringComparison.Ordinal);
    }
}
