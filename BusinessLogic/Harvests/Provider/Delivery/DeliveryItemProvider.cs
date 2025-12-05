using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class DeliveryItemProvider
{
    private readonly FarmDbContext _db;

    public DeliveryItemProvider(FarmDbContext db) => _db = db;

    public async Task<DeliveryItem?> GetByIdAsync(int id) =>
        await _db.DeliveryItems
            .Include(di => di.Product)
            .Include(di => di.DeliveryPlan)
            .FirstOrDefaultAsync(di => di.Id == id);

    public async Task<List<DeliveryItem>> GetAllAsync() =>
        await _db.DeliveryItems
            .Include(di => di.Product)
            .Include(di => di.DeliveryPlan)
            .ToListAsync();

    public async Task AddAsync(DeliveryItem entity)
    {
        await _db.DeliveryItems.AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(DeliveryItem entity)
    {
        _db.DeliveryItems.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(DeliveryItem entity)
    {
        _db.DeliveryItems.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<List<DeliveryItem>> GetByPlanIdAsync(int planId) =>
        await _db.DeliveryItems
            .Where(di => di.DeliveryPlanId == planId)
            .Include(di => di.DeliveryPlan)
            .Include(di => di.Product)
            .ToListAsync();

    public async Task<DeliveryPlan?> GetByStoreAndDateAsync(int storeId, DateOnly datePlan) =>
        await _db.DeliveryPlans
            .Include(dp => dp.Items)
            .ThenInclude(i => i.Product)
            .Include(dp => dp.Store)
            .FirstOrDefaultAsync(dp =>
                dp.StoreId == storeId &&
                dp.DeliveryDate == datePlan);

    
    public async Task<List<DeliveryItem>> GetByStoreAndDateRangeAsync(int storeId, DateOnly dateMin, DateOnly dateMax) =>
        await _db.DeliveryItems
            .Where(di => di.DeliveryPlan.StoreId == storeId &&
                           (di.DeliveryPlan.DeliveryDate >= dateMin &&
                            di.DeliveryPlan.DeliveryDate <= dateMax))
            .Include(di => di.Product)
            .ToListAsync();

}