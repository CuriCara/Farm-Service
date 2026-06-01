using DataAccess.Entity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataAccess;

public static class FarmDbInitializer
{
    private static readonly Random _random = new Random(42);
    private const double CenterLatVor = 51.6720;
    private const double CenterLonVor = 39.175;
    
    public static async Task Seed(FarmDbContext context)
    {
        if (context.UnitCategories.Any()) return;
        
        var mass = new UnitCategory { Name = "масса" };
        var volume = new UnitCategory { Name = "объем" };
        var count = new UnitCategory { Name = "штуки" };

        await context.UnitCategories.AddRangeAsync(mass, volume, count);
        await context.SaveChangesAsync(); 
        
        var kg = new UnitsOfMeasurement { UoM = "Кг", ConversionFactor = 1.0, UnitCategoryId = mass.Id };
        var liter = new UnitsOfMeasurement { UoM = "Л", ConversionFactor = 1.0, UnitCategoryId = volume.Id };
        var piece = new UnitsOfMeasurement { UoM = "Шт", ConversionFactor = 1.0, UnitCategoryId = count.Id };
        var gramm = new UnitsOfMeasurement { UoM = "Г", ConversionFactor = 0.001, UnitCategoryId = mass.Id };
        var ml = new UnitsOfMeasurement { UoM = "Мл", ConversionFactor = 0.001, UnitCategoryId = volume.Id };

        await context.UnitsOfMeasurements.AddRangeAsync(kg, liter, piece, gramm, ml);
        await context.SaveChangesAsync();
        
        mass.BaseUnitId = kg.Id;
        volume.BaseUnitId = liter.Id;
        count.BaseUnitId = piece.Id;
        await context.SaveChangesAsync();
    }

    public static async Task CreateEntityAsync(FarmDbContext context, string osmConnectionString)
    {
        if (!context.Farms.Any())
        {
            var sqlFarms = @"
                            SELECT DISTINCT ON (lon, lat)
                              ST_X(ST_Transform(ST_PointOnSurface(f.way), 4326)) AS lon,
                              ST_Y(ST_Transform(ST_PointOnSurface(f.way), 4326)) AS lat
                            FROM planet_osm_polygon f
                            JOIN planet_osm_polygon city
                              ON ST_Intersects(f.way, city.way)
                            WHERE f.landuse LIKE 'farmyard'
                              AND city.name = 'Воронеж'
                              AND f.way IS NOT NULL
                              AND ST_IsValid(f.way);";
            
            var farms = new List<Farm>();
            
            using var conn = new NpgsqlConnection(osmConnectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(sqlFarms, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var cnt = 0;
            
            while (await reader.ReadAsync())
            {
                cnt++;
                var farmName = $"Farm_{cnt}";
                var lon = reader.IsDBNull(0) ? double.NaN : reader.GetDouble(0);
                var lat = reader.IsDBNull(1) ? double.NaN : reader.GetDouble(1);
                
                // Фильтр некорректных координат
                if (double.IsNaN(lat) || double.IsNaN(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180)
                    continue;
                
                farms.Add(new Farm
                {
                    Name = farmName,
                    Address = $"г. Воронеж, {farmName}",
                    Longitude = lon,
                    Latitude = lat
                });
            }

            if (farms.Any())
            {
                await context.Farms.AddRangeAsync(farms);
                await context.SaveChangesAsync();
                Console.WriteLine($"Загружено {farms.Count} ферм из OSM");
            }
            else
            {
                Console.WriteLine("Фермы не найдены");
            }
        }

        // Реальные магазины из OSM
        if (!context.Stores.Any())
        {
            var sqlStores = @"
                WITH ranked_points AS (
                    SELECT
                        p.name,
                        p.shop,
                        CONCAT_WS(', ',
                            p.tags->'addr:street',
                            p.tags->'addr:housenumber',
                            p.tags->'addr:postcode',
                            p.tags->'addr:city',
                            p.tags->'addr:country'
                        ) AS full_address,
                        ST_X(ST_Transform(p.way, 4326)) AS lon,
                        ST_Y(ST_Transform(p.way, 4326)) AS lat,
                        ROW_NUMBER() OVER (PARTITION BY
                            ROUND(ST_X(ST_Transform(p.way, 4326))::numeric, 6),
                            ROUND(ST_Y(ST_Transform(p.way, 4326))::numeric, 6)
                            ORDER BY p.osm_id) AS rn
                    FROM planet_osm_point p
                    JOIN planet_osm_polygon city ON ST_Intersects(p.way, city.way)
                    WHERE p.shop IS NOT NULL
                      AND (city.name ILIKE '%Воронеж%' OR city.tags->'name:ru' = 'Воронеж')
                      AND city.way IS NOT NULL
                      AND p.name IN ('Пятерочка','Пятёрочка','Магнит','Центрторг','Европа',
                                     'Перекресток','Перекрёсток','Твой Дом','Ашан','Окей')
                )
                SELECT name, shop, full_address, lon, lat
                FROM ranked_points
                WHERE rn = 1
                ORDER BY MD5(name || shop)  -- случайное распределение
                LIMIT 300;";

            var stores = new List<Store>();
            
            using var conn = new NpgsqlConnection(osmConnectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(sqlStores, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var storeName = reader.IsDBNull(0) ? null : reader.GetString(0);
                var shopType = reader.IsDBNull(1) ? null : reader.GetString(1);
    
                // Адрес магазина
                var fullAddress = reader.IsDBNull(2) ? null : reader.GetString(2);
    
                // Координаты
                var lon = reader.IsDBNull(3) ? double.NaN : reader.GetDouble(3);
                var lat = reader.IsDBNull(4) ? double.NaN : reader.GetDouble(4);

                // Фильтр некорректных координат
                if (double.IsNaN(lat) || double.IsNaN(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180)
                    continue;

                stores.Add(new Store
                {
                    Name = string.IsNullOrWhiteSpace(storeName) ? $"Магазин ({shopType})" : storeName,
                    // Если адрес из OSM пустой - подставляем заглушку с городом и типом
                    Address = string.IsNullOrWhiteSpace(fullAddress) 
                        ? $"г. Воронеж, {shopType ?? "магазин"}" 
                        : fullAddress,
                    Latitude = lat,
                    Longitude = lon
                });
            }

            if (stores.Any())
            {
                await context.Stores.AddRangeAsync(stores);
                await context.SaveChangesAsync();
                Console.WriteLine($"Загружено {stores.Count} магазинов из OSM");
            }
            else
            {
                Console.WriteLine("Магазины не найдены");
            }
        }

        if (!context.Vehicles.Any())
        {
            var depotFarm = context.Farms.FirstOrDefault(f => f.Name == "Центральная ферма") 
                            ?? context.Farms.First();
    
            var vehicles = new List<Vehicle>();
    
            for (int i = 1; i <= 150; i++)
            {
                vehicles.Add(new Vehicle
                {
                    Name = $"Грузовик #{i:D2}",
                    Capacity = 3000.0,       
                    SpeedKmph = 60,           
                    CostPerKm = 15.0,         
                    StartPointId = depotFarm.Id,
                    StartDepot = depotFarm,
                    IsActive = true
                });
            }
    
            await context.Vehicles.AddRangeAsync(vehicles);
            await context.SaveChangesAsync();
        }

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
        await context.SaveChangesAsync();
        return;
    }

    public static async Task CreateDeliveryPlanForTestAsync(FarmDbContext _dbContext,
        DateOnly dateOnly)
    {
        if (_dbContext.DeliveryPlans.Any(p => p.DeliveryDate == dateOnly))
            return;

        var stores = await _dbContext.Stores.ToListAsync();

        if (!stores.Any())
            throw new Exception("Нету магазинов");
        
        var products = await _dbContext.Products.Take(3).ToListAsync();

        if (!products.Any())
            throw new Exception("Нету 3-х продуктов");

        var plans = new List<DeliveryPlan>();

        foreach (var store in stores)
        {
            var plan = new DeliveryPlan
            {
                DeliveryDate = dateOnly,
                StoreId = store.Id,
                IsCompleted = false,
                Items = new List<DeliveryItem>()
            };

            foreach (var product in products)
            {
                plan.Items.Add(new DeliveryItem
                {
                    ProductId = product.Id,
                    Quantity = _random.Next(0, 100)
                });
            }
            plans.Add(plan);
        }
        
        _dbContext.DeliveryPlans.AddRange(plans);
        await _dbContext.SaveChangesAsync();
    }
}