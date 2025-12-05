using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Logistics.Provider;

public class StoreDemandProvider
{
    private readonly FarmDbContext _db;

    public StoreDemandProvider(FarmDbContext db) => _db = db;

    public async Task<List<StoreDemand>> GetByDateAsync(DateOnly date) =>
        await _db.StoreDemands
            .Include(d => d.Store)
            .Include(d => d.Product)
            .Where(d => d.Date == date)
            .ToListAsync();

    public async Task<StoreDemand?> GetOneAsync(int storeId, int productId, DateOnly date) =>
        await _db.StoreDemands.FirstOrDefaultAsync(d =>
            d.StoreId == storeId &&
            d.ProductId == productId &&
            d.Date == date);

    public async Task AddOrUpdateAsync(StoreDemand demand)
    {
        var existing = await GetOneAsync(demand.StoreId, demand.ProductId, demand.Date);

        if (existing == null)
        {
            await _db.StoreDemands.AddAsync(demand);
        }
        else
        {
            existing.RequiredQuantity = demand.RequiredQuantity;
            existing.PlannedQuantity = demand.PlannedQuantity;
        }

        await _db.SaveChangesAsync();
    }

    public async Task UpdatePlannedQuantityAsync(int storeId, int productId, DateOnly date, double qty)
    {
        var demand = await GetOneAsync(storeId, productId, date);
        if (demand == null) return;

        demand.PlannedQuantity = qty;
        await _db.SaveChangesAsync();
    }
    
    public async Task UpdateFromDeliveryPlanAsync(DeliveryPlan plan)
    {
        foreach (var item in plan.Items)
        {
            var demand = await GetOneAsync(plan.StoreId, item.ProductId, plan.DeliveryDate);

            if (demand == null)
            {
                demand = new StoreDemand
                {
                    StoreId = plan.StoreId,
                    ProductId = item.ProductId,
                    Date = plan.DeliveryDate,
                    RequiredQuantity = 0,
                    PlannedQuantity = item.Quantity
                };
                await _db.StoreDemands.AddAsync(demand);
            }
            else
            {
                demand.PlannedQuantity = item.Quantity;
            }
        }

        await _db.SaveChangesAsync();
    }

}