using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Stores;


public class StoreService
{
    private readonly FarmDbContext _db;
    private readonly Random _random = new Random();

    private const int MinCount = 3;
    private const int MaxCount = 20;
    private DateOnly DefaultDate = DateOnly.FromDateTime(DateTime.Today.Date);

    public StoreService(FarmDbContext db) => _db = db;

    public async Task RandomPlanGenerateAsync(int storeId, DateOnly date)
    {
        var products = await _db.Products.Include(p => p.Category)
            .ThenInclude(c => c.BaseUnit)
            .ToListAsync();

        int itemCount = _random.Next(2, products.Count + 1);
        
        var selectProducts = products.OrderBy(_ => Guid.NewGuid())
            .Take(itemCount)
            .ToList();

        var plan = new DeliveryPlan
        {
            StoreId = storeId,
            DeliveryDate = date == DateOnly.FromDateTime(DateTime.MaxValue)? DefaultDate : date,
            Items = new List<DeliveryItem>()
        };

        foreach (var product in selectProducts)
        {
            int rawAmount = _random.Next(MinCount, MaxCount + 1);

            var baseUnit = product.Category.BaseUnit;

            double convertQuant = rawAmount * baseUnit.ConversionFactor;
            
            plan.Items.Add(new DeliveryItem
            {
                ProductId = product.Id,
                Quantity = convertQuant
            });
        }
        
        await _db.DeliveryPlans.AddAsync(plan);
        await _db.SaveChangesAsync();
    }
}