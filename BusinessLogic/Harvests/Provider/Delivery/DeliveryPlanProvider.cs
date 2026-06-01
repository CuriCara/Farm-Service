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

    public async Task<DeliveryPlan?> GetByIdWithItemsAsync(int planId) =>
        await _db.DeliveryPlans
            .Include(dp => dp.Store)
            .Include(dp => dp.Items)
            .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Category)
            .ThenInclude(c => c.BaseUnit)
            .FirstOrDefaultAsync(dp => dp.Id == planId);
    public async Task CreateRandomByStoreAsync(int storeId)
    {
        await _storeService.RandomPlanGenerateAsync(storeId, DateOnly.FromDateTime(DateTime.MaxValue));
    }
    
    public async Task CreateRandomByStoreAndDateAsync(int storeId, DateOnly date)
    {
        await _storeService.RandomPlanGenerateAsync(storeId, date);
        var plan = await GetByStoreAndDateAsync(storeId, date);
        if (plan != null)
            await _storeDemand.UpdateFromDeliveryPlanAsync(plan);
    }
    
    public async Task AddAsync(DeliveryPlan entity)
    {
        await _db.DeliveryPlans.AddAsync(entity);
        await _db.SaveChangesAsync();
        await _storeDemand.UpdateFromDeliveryPlanAsync(entity);
    }

    public async Task AddItemToPlanAsync(int planId, int productId, double quantity)
    {
        var plan = await _db.DeliveryPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == planId);

        if (plan == null)
            throw new InvalidOperationException("Такой план не найден");

        if (plan.Items?.Any(i => i.Id == productId) == true)
            throw new InvalidOperationException("Такой товар уже добавлен");

        var product = await _db.Products.FindAsync(productId);
        if (product == null)
            throw new InvalidOperationException("Такого товара не существует");

        var newDeliveryItem = new DeliveryItem
        {
            ProductId = productId,
            DeliveryPlanId = planId,
            Quantity = quantity
        };

        await _db.DeliveryItems.AddAsync(newDeliveryItem);
        await _db.SaveChangesAsync();

        var newPlan = newDeliveryItem.DeliveryPlan;
        
        await _storeDemand.UpdateFromDeliveryPlanAsync(newPlan);
    }

    public async Task UpdateAsync(DeliveryPlan entity)
    {
        _db.DeliveryPlans.Update(entity);
        await _db.SaveChangesAsync();
        await _storeDemand.UpdateFromDeliveryPlanAsync(entity);
    }

    public async Task UpdateWithNewQuantityAsync(int itemId, int quantity)
    {
        var item = await _db.DeliveryItems
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
            throw new InvalidOperationException("Товара нету в списке плана");

        item.Quantity = quantity;
        await _db.SaveChangesAsync();

        var plan = item.DeliveryPlan;

        await _storeDemand.UpdateFromDeliveryPlanAsync(plan);
    }
    public async Task DeleteAsync(DeliveryPlan entity)
    {
        _db.DeliveryPlans.Remove(entity);
        await _db.SaveChangesAsync();
        await _storeDemand.UpdateFromDeliveryPlanAsync(entity);
    }

    public async Task RemoveItemFromPlanAsync(int itemId)
    {
        var item = await _db.DeliveryItems
            .FindAsync(itemId);

        if (item == null)
            throw new InvalidOperationException("Товар не найден в плане");

        var plan = item.DeliveryPlan;
        
        _db.DeliveryItems.Remove(item);
        await _db.SaveChangesAsync();

        await _storeDemand.UpdateFromDeliveryPlanAsync(plan);
    }
}