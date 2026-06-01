using BusinessLogic.SubServices.Logistics.DTO;
using DataAccess;
using Tests.Logistics.Helper;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess.Entity;
using DataAccess.Entity.Logistics.GA;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Logistics.Helper;
using Tests.Logistics.IntegrationTests;

namespace Tests.IntegrationTests;

[CollectionDefinition("LogisticsTests")]
public class LogisticsTestCollection : ICollectionFixture<LogisticsTestFixture> { }

[Collection("LogisticsTests")]
public class DeliveryOptimizationServiceTests : IAsyncLifetime
{
    private readonly LogisticsTestFixture _fixture;
    private readonly DeliveryOptimizationService _service;
    private readonly IDbContextFactory<FarmDbContext> _contextFactory;

    public DeliveryOptimizationServiceTests(LogisticsTestFixture fixture)
    {
        _fixture = fixture;
        _service = fixture.ServiceProvider.GetRequiredService<DeliveryOptimizationService>();
        _contextFactory = fixture.ServiceProvider.GetRequiredService<IDbContextFactory<FarmDbContext>>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync(); // очищаем БД перед каждым тестом
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OptimizeRouteAsync_ShouldLoadDataAndCreateTasksCorrectly()
    {
        using var context = _contextFactory.CreateDbContext();
        await TestDataSeeder.CreateTestDeliveryPlansAsync(context, new DateOnly(2026, 4, 19));

        var request = new RouteOptimizationRequestDTO
        {
            DeliveryDate = new DateOnly(2026, 4, 19),
            StoreIds = new List<int> { 1001, 1002, 1003 },
            DepotId = 1,
        };

        var result = await _service.OptimizeRouteAsync(request);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Routes);
        Assert.NotNull(result.Metrics);
        Assert.True(result.Metrics.TotalDistance >= 0);
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldHandleNullItemsSafely()
    {
        using var context = _contextFactory.CreateDbContext();

        // Создаём план с null Items
        context.DeliveryPlans.Add(new DeliveryPlan
        {
            DeliveryDate = new DateOnly(2026, 4, 19),
            StoreId = 9999,
            Items = null!
        });
        await context.SaveChangesAsync();

        var request = new RouteOptimizationRequestDTO
        {
            DeliveryDate = new DateOnly(2026, 4, 19),
            StoreIds = new List<int> { 9999, 1001 },
            DepotId = 1
        };

        var result = await _service.OptimizeRouteAsync(request);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Routes); // должен обработать и вернуть результат
    }
}