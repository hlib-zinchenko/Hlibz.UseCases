namespace Hlibz.UseCases.Tests;

public sealed class PipelineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_WithValidUseCase_RunsHandler()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto> result = await scope.ServiceProvider
            .GetRequiredService<ICreateCityCommandHandler>()
            .HandleAsync(CreateCityCommand.Valid(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("Kyiv", result.Value.Name);
        Assert.Equal(["handler"], provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidUseCase_ReturnsInvalidKeyedByPathWithoutRunningHandler()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto> result = await scope.ServiceProvider
            .GetRequiredService<ICreateCityCommandHandler>()
            .HandleAsync(
                new CreateCityCommand(
                    string.Empty,
                    2000,
                    new Household(0, 1),
                    [new CostLine(5, "OK"), new CostLine(-1, "low")]),
                Token);

        ValidationFailed failure = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(
            ["Name", "MagnitudeKm", "Household.Adults", "Costs[1].Value", "Costs[1].Note"],
            failure.Errors.Keys);
        Assert.Equal(["The Name field is required."], failure.Errors["Name"]);
        Assert.Empty(provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithValidationDisabled_RunsHandlerOnInvalidUseCase()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCases(
            [],
            options => options.Validation.Enabled = false));
        using IServiceScope scope = provider.CreateScope();

        Result result = await scope.ServiceProvider
            .GetRequiredService<IDeleteCityCommandHandler>()
            .HandleAsync(new DeleteCityCommand(0), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(["handler"], provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithFilters_RunsThemInRegistrationOrderAroundHandler()
    {
        using ServiceProvider provider = Services.Build(services => services
            .AddUseCaseFilter<FirstFilter>()
            .AddUseCaseFilter<SecondFilter>());
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto> result = await scope.ServiceProvider
            .GetRequiredService<ICreateCityCommandHandler>()
            .HandleAsync(CreateCityCommand.Valid(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                "first:before:CreateCityCommand",
                "second:before:CreateCityCommand",
                "handler",
                "second:after:Success",
                "first:after:Success",
            ],
            provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidUseCase_DoesNotRunFilters()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCaseFilter<FirstFilter>());
        using IServiceScope scope = provider.CreateScope();

        Result result = await scope.ServiceProvider
            .GetRequiredService<IDeleteCityCommandHandler>()
            .HandleAsync(new DeleteCityCommand(0), Token);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Empty(provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithFilterFailing_ReturnsItsFailureAsHandlerResultType()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCaseFilter<ForbiddingFilter>());
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto> result = await scope.ServiceProvider
            .GetRequiredService<ICreateCityCommandHandler>()
            .HandleAsync(CreateCityCommand.Valid(), Token);

        Assert.IsType<Forbidden>(result.Error);
        Assert.Empty(provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithFilterReturningSuccessWithoutNext_Throws()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCaseFilter<SuccessWithoutNextFilter>());
        using IServiceScope scope = provider.CreateScope();
        ICreateCityCommandHandler handler = scope.ServiceProvider.GetRequiredService<ICreateCityCommandHandler>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(CreateCityCommand.Valid(), Token));
    }

    [Fact]
    public async Task HandleAsync_WithFilterAndNoResultHandler_PassesHandlerFailureThrough()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCaseFilter<FirstFilter>());
        using IServiceScope scope = provider.CreateScope();

        Result result = await scope.ServiceProvider
            .GetRequiredService<IDeleteCityCommandHandler>()
            .HandleAsync(new DeleteCityCommand(404), Token);

        Assert.Equal(new CityNotFound(404), result.Error);
        Assert.Equal(
            ["first:before:DeleteCityCommand", "handler", "first:after:NotFound"],
            provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithUseCaseWithoutRules_RunsHandler()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();

        Result<string?> result = await scope.ServiceProvider
            .GetRequiredService<IUseCaseHandler<PingQuery, string?>>()
            .HandleAsync(new PingQuery("pong"), Token);

        Assert.Equal("pong", result.Value);
    }

    [Fact]
    public async Task HandleAsync_WithDeclaredErrors_ReturnsValue()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto, RenameCityError> result = await scope.ServiceProvider
            .GetRequiredService<IRenameCityCommandHandler>()
            .HandleAsync(new RenameCityCommand(1, "Odesa"), Token);

        Assert.Equal(new CityDto("Odesa"), result.Value);
    }

    [Fact]
    public async Task HandleAsync_WithDeclaredErrorReturned_KeepsItAsDeclaredError()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto, RenameCityError> result = await scope.ServiceProvider
            .GetRequiredService<IRenameCityCommandHandler>()
            .HandleAsync(new RenameCityCommand(1, "Lviv"), Token);

        string described = result.DeclaredError switch
        {
            CityNotFound notFound => $"missing {notFound.CityId}",
            CityNameTaken taken => $"taken {taken.Name}",
            null => "none",
        };
        Assert.Equal("taken Lviv", described);
    }

    [Fact]
    public async Task HandleAsync_WithDeclaredErrorsAndInvalidUseCase_ReturnsValidationFailedWithoutRunningHandler()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto, RenameCityError> result = await scope.ServiceProvider
            .GetRequiredService<IRenameCityCommandHandler>()
            .HandleAsync(new RenameCityCommand(0, string.Empty), Token);

        ValidationFailed failure = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(["Id", "Name"], failure.Errors.Keys);
        Assert.Null(result.DeclaredError);
        Assert.Empty(provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithDeclaredErrorsAndFilterPassingThrough_KeepsDeclaredError()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCaseFilter<FirstFilter>());
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto, RenameCityError> result = await scope.ServiceProvider
            .GetRequiredService<IRenameCityCommandHandler>()
            .HandleAsync(new RenameCityCommand(404, "Odesa"), Token);

        Assert.True(result.DeclaredError is CityNotFound { CityId: 404 });
        Assert.Equal(
            ["first:before:RenameCityCommand", "handler", "first:after:NotFound"],
            provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithDeclaredErrorsAndFilterFailingAmbiently_ReturnsItsFailure()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCaseFilter<ForbiddingFilter>());
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto, RenameCityError> result = await scope.ServiceProvider
            .GetRequiredService<IRenameCityCommandHandler>()
            .HandleAsync(new RenameCityCommand(1, "Odesa"), Token);

        Assert.IsType<Forbidden>(result.Error);
        Assert.Null(result.DeclaredError);
        Assert.Empty(provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithDeclaredErrorsAndFilterFailingWithUndeclaredError_Throws()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCaseFilter<UndeclaredErrorFilter>());
        using IServiceScope scope = provider.CreateScope();
        IRenameCityCommandHandler handler = scope.ServiceProvider.GetRequiredService<IRenameCityCommandHandler>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new RenameCityCommand(1, "Odesa"), Token));

        Assert.Contains("UndeclaredError", exception.Message, StringComparison.Ordinal);
        Assert.Contains("RenameCityError", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WithUndeclaredErrorsAndFilterFailingWithAnyError_ReturnsIt()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCaseFilter<UndeclaredErrorFilter>());
        using IServiceScope scope = provider.CreateScope();

        Result<CityDto> result = await scope.ServiceProvider
            .GetRequiredService<ICreateCityCommandHandler>()
            .HandleAsync(CreateCityCommand.Valid(), Token);

        Assert.IsType<UndeclaredError>(result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithDeclaredErrorsAndUnit_ReturnsSuccessOrDeclaredError()
    {
        using ServiceProvider provider = Services.Build();
        using IServiceScope scope = provider.CreateScope();
        IUseCaseHandler<ArchiveCityCommand, Unit, RenameCityError> handler = scope.ServiceProvider
            .GetRequiredService<IUseCaseHandler<ArchiveCityCommand, Unit, RenameCityError>>();

        Result<Unit, RenameCityError> archived = await handler.HandleAsync(new ArchiveCityCommand(1), Token);
        Result<Unit, RenameCityError> missing = await handler.HandleAsync(new ArchiveCityCommand(404), Token);

        Assert.True(archived.IsSuccess);
        Assert.True(missing.DeclaredError is CityNotFound);
    }
}
