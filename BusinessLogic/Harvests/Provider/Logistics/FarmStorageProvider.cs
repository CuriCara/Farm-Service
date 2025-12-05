using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Logistics.Provider;

public class FarmStorageProvider
{
    private readonly FarmDbContext _db;
    public FarmStorageProvider(FarmDbContext db) => _db = db;

    public async Task<List<FarmStorage>> GetAllAsync() =>
        await _db.FarmStorages
            .Include(s => s.Farm)
            .Include(s => s.Product)
            .ToListAsync();

    public async Task<FarmStorage?> GetByFarmAndProductAsync(int farmId, int productId) =>
        await _db.FarmStorages
            .FirstOrDefaultAsync(s =>
                s.FarmId == farmId &&
                s.ProductId == productId);

    public async Task UpdateStorageAfterHarvestAsync(Harvest harvest, double baseQuantity)
    {
        var storage = await GetByFarmAndProductAsync(harvest.FarmId, harvest.ProductId);

        if (storage == null)
        {
            storage = new FarmStorage
            {
                FarmId = harvest.FarmId,
                ProductId = harvest.ProductId,
                Quantity = baseQuantity
            };

            await _db.FarmStorages.AddAsync(storage);
        }
        else
        {
            storage.Quantity += baseQuantity;
        }

        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(FarmStorage storage)
    {
        _db.FarmStorages.Update(storage);
        await _db.SaveChangesAsync();
    }
}