namespace Hlibz.UseCases.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void AddUseCases_WithAssembly_RegistersEveryHandlerUnderEachOfItsInterfaces()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ICreateCityCommandHandler own = services.GetRequiredService<ICreateCityCommandHandler>();
        Assert.Same(own, services.GetRequiredService<IUseCaseHandler<CreateCityCommand, CityDto>>());
        Assert.IsNotType<CreateCityCommandHandler>(own);
        Assert.NotNull(services.GetRequiredService<IDeleteCityCommandHandler>());
        Assert.NotNull(services.GetRequiredService<IUseCaseHandler<PingQuery, string?>>());
    }

    [Fact]
    public void AddUseCases_WithAssembly_ResolvesEveryHandlerThroughMarker()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();

        IUseCaseHandler[] handlers = [.. scope.ServiceProvider.GetServices<IUseCaseHandler>()];

        Assert.Equal(
            provider.GetRequiredService<IUseCaseCatalog>().UseCases.Count,
            handlers.Length);
    }

    [Fact]
    public void Catalog_WithAssembly_DescribesEachUseCase()
    {
        using ServiceProvider provider = Services.Build();
        IUseCaseCatalog catalog = provider.GetRequiredService<IUseCaseCatalog>();

        Assert.Equal(
            new UseCaseDescriptor(typeof(CreateCityCommand), typeof(CreateCityCommandHandler), typeof(CityDto), ErrorSetType: null, CanFailValidation: true),
            catalog.Find(typeof(CreateCityCommand)));
        Assert.Equal(
            new UseCaseDescriptor(typeof(DeleteCityCommand), typeof(DeleteCityCommandHandler), ResultType: null, ErrorSetType: null, CanFailValidation: true),
            catalog.Find(typeof(DeleteCityCommand)));
        Assert.Equal(
            new UseCaseDescriptor(typeof(RenameCityCommand), typeof(RenameCityCommandHandler), typeof(CityDto), typeof(RenameCityError), CanFailValidation: true),
            catalog.Find(typeof(RenameCityCommand)));
        Assert.Equal(
            new UseCaseDescriptor(typeof(ArchiveCityCommand), typeof(ArchiveCityCommandHandler), typeof(Unit), typeof(RenameCityError), CanFailValidation: false),
            catalog.Find(typeof(ArchiveCityCommand)));
        Assert.False(catalog.Find(typeof(PingQuery))!.CanFailValidation);
        Assert.Null(catalog.Find(typeof(string)));
    }

    [Fact]
    public void AddUseCases_CalledTwiceWithSameAssembly_RegistersHandlersOnce()
    {
        ServiceCollection services = new();
        services.AddSingleton<CallLog>();
        services.AddUseCases([typeof(Services).Assembly]);
        int count = services.Count;

        services.AddUseCases([typeof(Services).Assembly]);

        Assert.Equal(count, services.Count);
    }

    [Fact]
    public void AddUseCases_WithAssemblyNotCompiledWithGenerator_Throws()
    {
        ServiceCollection services = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddUseCases([typeof(object).Assembly]));

        Assert.Contains("source generator", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProvider_WithZeroMaxDepth_FailsOnFirstUse()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCases(
            [],
            options => options.Validation.MaxDepth = 0));
        using IServiceScope scope = provider.CreateScope();

        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(
            scope.ServiceProvider.GetRequiredService<ICreateCityCommandHandler>);
    }
}
