using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class HarvestProvider : IRepository<Harvest>
{
    private readonly FarmDbContext _db;
    public HarvestProvider(FarmDbContext db) { _db = db; }

    public async Task<List<Harvest>> GetAllAsync() => 
        await _db.Harvests
            .Include(h => h.Unit)
            .ThenInclude(u => u.Category)
            .ThenInclude(c => c.BaseUnit)
            .Include(h => h.Product)
            .Include(h => h.User)
            .ToListAsync();

    public async Task<Harvest> GetByIdAsync(int id) =>
        await _db.Harvests.Include(h => h.User).Include(h => h.Product)
            .FirstOrDefaultAsync(h => h.Id == id);

    public async Task AddAsync(Harvest entity)
    {
        _db.Harvests.Add(entity);
        await _db.SaveChangesAsync();
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