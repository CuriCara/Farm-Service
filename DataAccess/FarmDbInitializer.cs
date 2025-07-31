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

        context.UnitsOfMeasurements.AddRange(kg, liter, piece);
        context.SaveChanges();
        
        mass.BaseUnitId = kg.Id;
        volume.BaseUnitId = liter.Id;
        count.BaseUnitId = piece.Id;
        context.SaveChanges();
        
        
    }
}