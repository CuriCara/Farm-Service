using BusinessLogic.Logistics.Provider;
using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;
using BusinessLogic.Stores;

namespace BusinessLogic.Harvests.Provider;

public class DeliveryPlanProvider
{
    private readonly FarmDbContext _db;
    private readonly StoreService _storeService;
    private readonly StoreDemandProvider _storeDemand;

    public DeliveryPlanProvider(FarmDbContext db, StoreService storeService, StoreDemandProvider storeDemand)
    {
        _db = db;
        _storeService = storeService;
        _storeDemand = storeDemand;
    }
    
    public async Task<DeliveryPlan?> GetByIdAsync (int planId) =>
        await _db.DeliveryPlans
            .FirstOrDefaultAsync(dp => dp.Id == planId);

    public async Task<DeliveryPlan?> GetByStoreAsync(int storeId) =>
        await _db.DeliveryPlans
            .FirstOrDefaultAsync(dp => dp.StoreId == storeId);

    public async Task<List<DeliveryPlan>> GetAllAsync() =>
        await _db.DeliveryPlans
            .Include(dp => dp.Store)
            .ToListAsync();

    public async Task<DeliveryPlan?> GetByStoreAndDateAsync(int storeId, DateOnly datePlan) =>
        await _db.DeliveryPlans
            .Include(dp => dp.Items)
            .ThenInclude(i => i.Product)
            .Include(dp => dp.Store)
            .FirstOrDefaultAsync(dp =>
                dp.StoreId == storeId &&
                dp.DeliveryDate == datePlan);
    
    public async Task<List<DeliveryPlan>> GetPlanWithItemsAsync() =>
        await _db.DeliveryPlans
            .Include(dp => dp.Items)
            .ThenInclude(i => i.Product)
            .Include(dp => dp.Store)
            .ToListAsync();
    

    public async Task CreateRandomByStoreAsync(int storeId)
    {
        await _storeService.RandomPlanGenerateAsync(storeId, DateOnly.FromDateTime(DateTime.MaxValue));
    }
    
    public async Task CreateRandomByStoreAndDateAsync(int storeId, DateOnly date)
    {
        await _storeService.RandomPlanGenerateAsync(storeId, date);
        var plan = await GetByStoreAndDateAsync(storeId, date);
        await _storeDemand.UpdateFromDeliveryPlanAsync(plan);
    }
    
    public async Task AddAsync(DeliveryPlan entity)
    {
        await _db.DeliveryPlans.AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(DeliveryPlan entity)
    {
        _db.DeliveryPlans.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(DeliveryPlan entity)
    {
        _db.DeliveryPlans.Remove(entity);
        await _db.SaveChangesAsync();
    }
}