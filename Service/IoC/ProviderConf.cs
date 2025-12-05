using BusinessLogic.EmailSend;
using BusinessLogic.Harvests.Manager;
using BusinessLogic.Harvests.Provider;
using BusinessLogic.Logistics.Provider;
using BusinessLogic.Mapper;
using BusinessLogic.Stores;
using BusinessLogic.Stores.Provider;
using DataAccess;
using DataAccess.Entity;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using Service.GA.DistanceMatrix;

namespace Service.IoC;

public class ProviderConf
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        
        builder.Services.AddRazorPages();
        builder.Services.AddScoped<ProductManager>();
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
        builder.Services.AddScoped<GraphHopperDistanceMatrixProvider>();
        builder.Services.AddScoped<IRepository<Harvest>, HarvestProvider>();
        builder.Services.AddScoped<IRepository<User>, UserProvider>();
        builder.Services.AddScoped<IRepository<Product>, ProductProvider>();
        builder.Services.AddScoped<IRepository<UnitsOfMeasurement>, UnitsOfMeasurementProvider>();
        builder.Services.AddScoped<IRepository<DataAccess.Entity.Farm>, FarmProvider>();
    }
}