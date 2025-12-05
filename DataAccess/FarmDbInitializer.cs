using DataAccess.Entity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DataAccess;

public static class FarmDbInitializer 
{
    public static void Seed(FarmDbContext context)
    {
        if (context.UnitCategories.Any()) return;
        
        var mass = new UnitCategory { Name = "масса" };
        var volume = new UnitCategory { Name = "объем" };
        var count = new UnitCategory { Name = "штуки" };

        context.UnitCategories.AddRange(mass, volume, count);
        context.SaveChanges(); 
        
        var kg = new UnitsOfMeasurement { UoM = "Кг", ConversionFactor = 1.0, UnitCategoryId = mass.Id };
        var liter = new UnitsOfMeasurement { UoM = "Л", ConversionFactor = 1.0, UnitCategoryId = volume.Id };
        var piece = new UnitsOfMeasurement { UoM = "Шт", ConversionFactor = 1.0, UnitCategoryId = count.Id };
        var gramm = new UnitsOfMeasurement { UoM = "Г", ConversionFactor = 0.001, UnitCategoryId = mass.Id };
        var ml = new UnitsOfMeasurement { UoM = "Мл", ConversionFactor = 0.001, UnitCategoryId = volume.Id };

        context.UnitsOfMeasurements.AddRange(kg, liter, piece, gramm, ml);
        context.SaveChanges();
        
        mass.BaseUnitId = kg.Id;
        volume.BaseUnitId = liter.Id;
        count.BaseUnitId = piece.Id;
        context.SaveChanges();
    }

    public static void CreateEntity(FarmDbContext context)
    {
        if (context.Stores.Any()) return;
            context.Stores.AddRange(
                new Store
                {
                    Name = "Магнит",
                    Address = "ул. Карла Маркса, д. 98",
                    Latitude = 51.670918,
                    Longitude = 39.197590
                },
                new Store
                {
                    Name = "Перекресток",
                    Address = "бул. Олимпийсикй, д. 10",
                    Latitude = 51.712317,
                    Longitude = 39.201104
                },
                new Store
                {
                    Name = "Европа",
                    Address = "Лененский проспект, д. 95Б",
                    Latitude = 51.664335,
                    Longitude = 39.256826
                },
                new Store
                {
                    Name = "Лента",
                    Address = "ул. Домостроителей, д. 24",
                    Latitude = 51.652830,
                    Longitude = 39.148348
                },
                new Store
                {
                    Name = "Твой Дом",
                    Address = "Монтажный проезд, д. 2",
                    Latitude = 51.653114,
                    Longitude = 39.291633
                }
            );

        if (context.Farms.Any()) return;
        
            context.Farms.AddRange(
                new Farm
                {
                    Name = "Северная ферма",
                    Address = "с. Новоживатинное, ул. Шоссейная, д. 14А",
                    Latitude = 51.881826,
                    Longitude = 39.188076
                },
                new Farm
                {
                    Name = "Восточная ферма",
                    Address = "с. Подклетное, ул. Солнечная, д. 19",
                    Latitude = 51.586930,
                    Longitude = 39.460953
                },
                new Farm
                {
                    Name = "Западная ферма",
                    Address = "Семилуки, ул. Воронежская, д. 300",
                    Latitude = 51.654871,
                    Longitude = 39.063858
                }
            );
        

        if (context.Products.Any()) return;
        
            context.Products.AddRange(
                new Product
                {
                    ProductName = "Яблоки",
                    UnitCategoryId = 1
                },
                new Product
                {
                    ProductName = "Морковь",
                    UnitCategoryId = 1
                },
                new Product
                {
                    ProductName = "Молоко",
                    UnitCategoryId = 2
                },
                new Product
                {
                    ProductName = "Яйца куриные",
                    UnitCategoryId = 3
                },
                new Product
                {
                    ProductName = "Сок яблочный",
                    UnitCategoryId = 2
                },
                new Product
                {
                    ProductName = "Головка сыра",
                    UnitCategoryId = 3
                }
            );
        context.SaveChanges();
    }
}