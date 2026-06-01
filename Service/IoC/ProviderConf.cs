using BusinessLogic.EmailSend;
using BusinessLogic.GraphHopper.DistanceMatrix;
using BusinessLogic.GraphHopper.DistanceMatrix.Cache;
using BusinessLogic.Harvests.Manager;
using BusinessLogic.Harvests.Provider;
using BusinessLogic.Logistics.Provider;
using BusinessLogic.Mapper;
using BusinessLogic.Stores;
using BusinessLogic.Stores.Provider;
using BusinessLogic.SubServices.Logistics;
using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.GA.Config;
using DataAccess;
using DataAccess.Entity;
using DataAccess.Entity.GrH;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Service.IoC;

public class ProviderConf
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        
        builder.Services.AddRazorPages();
        builder.Services.AddScoped<UnitsOfMeasurementProvider>();
        builder.Services.AddScoped<UnitCategoryProvider>();
        //builder.Services.AddScoped<IAuthProvider, AuthProvider>();
        builder.Services.AddScoped<HarvestManager>();
        builder.Services.AddScoped<ProductProvider>();
        builder.Services.AddScoped<UserProvider>();
        builder.Services.AddScoped<EmailService>();
        builder.Services.AddScoped<ReportService>();
        builder.Services.AddScoped<FarmProvider>();
        builder.Services.AddScoped<StoreService>();
        builder.Services.AddScoped<StoreProvider>();
        builder.Services.AddScoped<StoreProductProvider>();
        builder.Services.AddScoped<DeliveryItemProvider>();
        builder.Services.AddScoped<DeliveryPlanProvider>();
        builder.Services.AddScoped<FarmStorageProvider>();
        builder.Services.AddScoped<RouteProvider>();
        builder.Services.AddScoped<RoutePlanProvider>();
        builder.Services.AddScoped<RouteStopProvider>();
        builder.Services.AddScoped<StoreDemandProvider>();
        builder.Services.AddScoped<VehicleProvider>();
        // Конфигурация GraphHopper - регистрируем как Singleton
        var graphHopperConfig = builder.Configuration
            .GetSection("GraphHopper")
            .Get<GraphHopperConfig>() ?? new GraphHopperConfig();
        
        if (graphHopperConfig.Servers == null || graphHopperConfig.Servers.Count == 0)
        {
            // Fallback на старый адрес если конфигурация не задана
            graphHopperConfig.Servers = new List<string> { "http://10.215.129.30:8989" };
        }
        
        builder.Services.AddSingleton(graphHopperConfig);
        
        builder.Services.AddSingleton<IServerSelector>(sp =>
        {
            var config = sp.GetRequiredService<GraphHopperConfig>();
            return new RoundRobinServerSelector(config.Servers);
        });
        
        builder.Services.AddHttpClient<GraphHopperElementWiseMatrixProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(graphHopperConfig.RequestTimeoutSeconds);
        });
        
        builder.Services.AddScoped<GraphHopperElementWiseMatrixProvider>();
        //builder.Services.AddSingleton<IDistanceCache,InMemoryDistanceCache>();
        builder.Services.AddScoped<IRepository<Harvest>, HarvestProvider>();
        builder.Services.AddScoped<IRepository<User>, UserProvider>();
        builder.Services.AddScoped<IRepository<Product>, ProductProvider>();
        builder.Services.AddScoped<IRepository<UnitsOfMeasurement>, UnitsOfMeasurementProvider>();
        builder.Services.AddScoped<IRepository<DataAccess.Entity.Farm>, FarmProvider>();
        builder.Services.AddSingleton<RedisDistanceCache>();
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = builder.Configuration["CacheSettings:RedisConnectionString"];
            var options = ConfigurationOptions.Parse(config);
            options.SyncTimeout = 30000; // 30 секунд
            options.AsyncTimeout = 30000;
            options.ConnectTimeout = 30000;
            return ConnectionMultiplexer.Connect(options);
        });builder.Services.AddSingleton<IDistanceCache, HybridDistanceCache>();
        builder.Services.AddTransient<IDistanceMatrixProvider>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(nameof(GraphHopperElementWiseMatrixProvider));
            var cache = sp.GetRequiredService<IDistanceCache>();
            var serverSelector = sp.GetRequiredService<IServerSelector>();
            var config = builder.Configuration
                .GetSection("GraphHopper")
                .Get<GraphHopperConfig>() ?? new GraphHopperConfig();
            
            return new GraphHopperElementWiseMatrixProvider(http, cache, serverSelector, config);
        });
        builder.Services.AddScoped<IDecodingContext, DecodingContext>();
        builder.Services.AddScoped<RouteDecoder>();
        builder.Services.AddScoped<IRouteDecoder>(sp => sp.GetRequiredService<RouteDecoder>());
        builder.Services.AddScoped<GeneticAlgorithm>();
        builder.Services.AddScoped<IGeneticAlgorithm>(sp => sp.GetRequiredService<GeneticAlgorithm>());
        builder.Services.AddOptions<GeneticAlgorithmConfig>()
            .BindConfiguration("GeneticAlgorithm")
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        builder.Services.AddScoped<DeliveryOptimizationService>(sp =>
        {
            var db = sp.GetRequiredService<FarmDbContext>();
            var matrix = sp.GetRequiredService<IDistanceMatrixProvider>();
            var cache = sp.GetRequiredService<IDistanceCache>();
            var logger = sp.GetService<ILogger<DeliveryOptimizationService>>();
            var gaLogger = sp.GetService<ILogger<GeneticAlgorithm>>();

            return new DeliveryOptimizationService(
                db, matrix, cache,
                fitnessObjective: FitnessObjective.MinimizeDistance, 
                logger: logger,
                gaConfig: null
            );
        });
        
        builder.Services.AddScoped<DistanceMatrixPreloadService>();
    }
}