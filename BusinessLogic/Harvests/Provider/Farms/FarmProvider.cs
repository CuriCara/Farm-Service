using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class FarmProvider : IRepository<Farm>
{
    private readonly FarmDbContext _db;

    public FarmProvider(FarmDbContext db)
    {
        _db = db;
    }

    public async Task<List<Farm>> GetAllAsync() =>
        await _db.Farms
            .Include(f => f.Harvests)
            .ToListAsync();

    public async Task<Farm> GetByIdAsync(int id) =>
        await _db.Farms.Include(f => f.Harvests)
            .FirstOrDefaultAsync(h => h.Id == id);

    public async Task AddAsync(Farm entity)
    {
        _db.Farms.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Farm entity)
    {
        _db.Farms.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Farm entity)
    {
        _db.Farms.Remove(entity);
        await _db.SaveChangesAsync();
    }
}    