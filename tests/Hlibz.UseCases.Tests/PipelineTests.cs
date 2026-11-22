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

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(
            ["Name", "MagnitudeKm", "Household.Adults", "Costs[1].Value", "Costs[1].Note"],
            result.ValidationErrors!.Keys);
        Assert.Equal(["The Name field is required."], result.ValidationErrors["Name"]);
        Assert.Empty(provider.GetRequiredService<CallLog>().Entries);
    }

    [Fact]
    public async Task HandleAsync_WithErrorFailureStatus_ReturnsErrorWithValidationErrors()
    {
        using ServiceProvider provider = Services.Build(services => services.AddUseCases(
            [],
            options =>
            {
                options.Validation.FailureStatus = ResultStatus.Error;
                options.Validation.FailureMessage = "Check the fields.";
            }));
        using IServiceScope scope = provider.CreateScope();

        Result result = await scope.ServiceProvider
            .GetRequiredService<IDeleteCityCommandHandler>()
            .HandleAsync(new DeleteCityCommand(0), Token);

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Equal("Check the fields.", result.ErrorMessage);
        Assert.Equal(["Id"], result.ValidationErrors!.Keys);
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

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal("Not for you.", result.ErrorMessage);
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

        Assert.Equal(ResultStatus.NotFound, result.Status);
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
}
