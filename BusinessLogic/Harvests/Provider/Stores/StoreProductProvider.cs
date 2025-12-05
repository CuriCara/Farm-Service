using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Stores.Provider;

public class StoreProductProvider
{
    private readonly FarmDbContext _db;

    public StoreProductProvider(FarmDbContext db)
    {
        _db = db;
    }

    public async Task<List<StoreProduct>> GetByStoreIdAsync(int storeId) =>
        await _db.StoreProducts
            .Include(sp => sp.Product)
            .Where(sp => sp.StoreId == storeId)
            .ToListAsync();

    public async Task<StoreProduct?> GetAsync(int storeId, int productId) =>
        await _db.StoreProducts
            .FirstOrDefaultAsync(sp => sp.StoreId == storeId && sp.ProductId == productId);

    public async Task AddOrUpdateAsync(int storeId, int productId, int quantity)
    {
        var existing = await GetAsync(storeId, productId);

        if (existing != null)
        {
            existing.Quantity = quantity;
            _db.StoreProducts.Update(existing);
        }
        else
        {
            var newItem = new StoreProduct
            {
                StoreId = storeId,
                ProductId = productId,
                Quantity = quantity
            };
            _db.StoreProducts.Add(newItem);
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int storeProductId)
    {
        var item = await _db.StoreProducts.FindAsync(storeProductId);
        if (item != null)
        {
            _db.StoreProducts.Remove(item);
            await _db.SaveChangesAsync();
        }
    }
}