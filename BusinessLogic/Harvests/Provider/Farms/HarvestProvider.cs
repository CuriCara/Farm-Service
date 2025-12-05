using BusinessLogic.Logistics.Provider;
using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class HarvestProvider : IRepository<Harvest>
{
    private readonly FarmDbContext _db;
    private readonly FarmStorageProvider _farmStorage;

    public HarvestProvider(FarmDbContext db, FarmStorageProvider farmStorage)
    {
        _db = db;
        _farmStorage = farmStorage;
    }

    public async Task<List<Harvest>> GetAllAsync() => 
        await _db.Harvests
            .Include(h => h.Unit)
                .ThenInclude(u => u.Category)
                .ThenInclude(c => c.BaseUnit)
            .Include(h => h.Product)
            .Include(h => h.User)
            .Include(h => h.Farm)
            .ToListAsync();

    public async Task<Harvest> GetByIdAsync(int id) =>
        await _db.Harvests
            .Include(h => h.Unit)
                .ThenInclude(u => u.Category)
                .ThenInclude(c => c.BaseUnit)
            .Include(h => h.User)
            .Include(h => h.Product)
            .Include(h => h.Farm)
            .FirstOrDefaultAsync(h => h.Id == id);

    public async Task AddAsync(Harvest entity)
    {
        var farmExists = await _db.Farms.AnyAsync(f => f.Id == entity.FarmId);
        if (!farmExists)
            throw new InvalidOperationException($"Farm with ID={entity.FarmId} does not exist.");

        _db.Harvests.Add(entity);
        await _db.SaveChangesAsync();

        var unit = await _db.UnitsOfMeasurements
            .Include(u => u.Category)
            .ThenInclude(c => c.BaseUnit)
            .FirstAsync(u => u.Id == entity.UnitId);

        double baseQuantity = entity.Quantity * unit.ConversionFactor;

        await _farmStorage.UpdateStorageAfterHarvestAsync(entity, baseQuantity);
    }
    public async Task UpdateAsync(Harvest entity)
    {
        _db.Harvests.Update(entity);
        await _db.SaveChangesAsync();
    }
    public async Task DeleteAsync(Harvest entity)
    {
        _db.Harvests.Remove(entity);
        await _db.SaveChangesAsync();
    }
}