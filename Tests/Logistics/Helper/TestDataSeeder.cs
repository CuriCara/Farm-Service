using DataAccess;
using DataAccess.Entity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Tests.Logistics.Helper;

public static class TestDataSeeder
{
    public static async Task SeedMinimalDataAsync(FarmDbContext context)
    {
        if (!await context.Farms.AnyAsync())
        {
            context.Farms.Add(new DataAccess.Entity.Farm
            {
                Id = 1,
                Name = "Центральная ферма",
                Latitude = 51.6720,
                Longitude = 39.1750
            });
        }

        if (!await context.Stores.AnyAsync())
        {
            context.Stores.AddRange(
                new Store { Id = 1001, Name = "Магнит #1", Latitude = 51.6800, Longitude = 39.1900 },
                new Store { Id = 1002, Name = "Пятёрочка #2", Latitude = 51.6650, Longitude = 39.1600 },
                new Store { Id = 1003, Name = "Перекрёсток #3", Latitude = 51.6750, Longitude = 39.2000 });
        }

        if (!await context.Products.AnyAsync())
        {
            context.Products.AddRange(
                new Product { Id = 1, ProductName = "Яблоки" },
                new Product { Id = 2, ProductName = "Молоко" });
        }

        if (!await context.Vehicles.AnyAsync())
        {
            context.Vehicles.Add(new Vehicle
            {
                Id = 1,
                Name = "Грузовик 1",
                Capacity = 3000,
                CostPerKm = 15,
                IsActive = true,
                StartPointId = 1
            });
        }

        await context.SaveChangesAsync();
    }

    public static async Task CreateTestDeliveryPlansAsync(FarmDbContext context, DateOnly date, int storeCount = 3)
    {
        var plans = new List<DeliveryPlan>();

        for (int i = 0; i < storeCount; i++)
        {
            var plan = new DeliveryPlan
            {
                DeliveryDate = date,
                StoreId = 1001 + i,
                IsCompleted = false,
                Items = new List<DeliveryItem>()
            };

            plan.Items.Add(new DeliveryItem { ProductId = 1, Quantity = 50 + i * 10 });
            plan.Items.Add(new DeliveryItem { ProductId = 2, Quantity = 30 + i * 5 });

            plans.Add(plan);
        }

        context.DeliveryPlans.AddRange(plans);
        await context.SaveChangesAsync();
    }
}