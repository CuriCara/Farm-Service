using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Respawn.Graph;
using Service.IoC;
using System;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.GraphHopper.DistanceMatrix;
using BusinessLogic.SubServices.Logistics.DTO;
using DataAccess;
using DataAccess.Entity.GrH;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tests.Logistics.Helper;
using Tests.Mocks;
using Xunit;

namespace Tests.Logistics.IntegrationTests;

public class LogisticsTestFixture : IAsyncLifetime
{
    public readonly IServiceProvider ServiceProvider;
    private Respawner? _respawner;

    public LogisticsTestFixture()
    {
        var services = new ServiceCollection();

        // 1. Создаём builder и вызываем ваши конфигурации
        var builder = WebApplication.CreateBuilder();
        DbContextConf.ConfigureService(builder);     // регистрирует Factory + Npgsql
        ProviderConf.ConfigureServices(builder);     // регистрирует всё остальное

        // 2. Копируем все сервисы из builder в нашу тестовую коллекцию
        foreach (var descriptor in builder.Services)
        {
            services.Add(descriptor);
        }

        // 3. Переопределяем тяжёлые зависимости на моки
        //var mockMatrix = new MockDistanceMatrixProvider(defaultDistanceKm: 8.0, defaultTimeSeconds: 480);
        //services.AddSingleton<IDistanceMatrixProvider>(mockMatrix);

        services.AddSingleton<IDistanceCache, MockDistanceCache>();

        // Удаляем старую регистрацию DeliveryOptimizationService (чтобы она взяла моки)
        var oldDelivery = services.FirstOrDefault(d => d.ServiceType == typeof(DeliveryOptimizationService));
        if (oldDelivery != null)
            services.Remove(oldDelivery);

        services.AddScoped<DeliveryOptimizationService>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        // Проверяем, что Factory теперь точно зарегистрирован
        var factory = ServiceProvider.GetRequiredService<IDbContextFactory<FarmDbContext>>();

        using var context = factory.CreateDbContext();

        _respawner = await Respawner.CreateAsync(context.Database.GetDbConnection(), new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new Table[]
            {
                "__EFMigrationsHistory",
                "AspNetRoles",
                "AspNetUsers",
                "AspNetUserRoles"
            }
        });

        await TestDataSeeder.SeedMinimalDataAsync(context);
    }

    public async Task ResetAsync()
    {
        if (_respawner == null) return;

        using var context = ServiceProvider
            .GetRequiredService<IDbContextFactory<FarmDbContext>>()
            .CreateDbContext();

        await _respawner.ResetAsync(context.Database.GetDbConnection());
    }

    public Task DisposeAsync() => Task.CompletedTask;
}