using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class UnitCategoryProvider
{
    private readonly FarmDbContext _db;

    public UnitCategoryProvider(FarmDbContext db)
    {
        _db = db;
    }

    public async Task<List<UnitCategory>> GetAllAsync() =>
        await _db.UnitCategories
            .Include(c => c.BaseUnit)
            .ToListAsync();
    public async Task<UnitCategory> GetByIdAsync(int id) =>
        await _db.UnitCategories
            .Include(c => c.BaseUnit)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddAsync(UnitCategory entity)
    {
        _db.UnitCategories.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(UnitCategory entity)
    {
        _db.UnitCategories.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(UnitCategory entity)
    {
        _db.UnitCategories.Remove(entity);
        await _db.SaveChangesAsync();
    }
}