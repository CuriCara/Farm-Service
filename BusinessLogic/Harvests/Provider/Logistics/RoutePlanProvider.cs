using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Logistics.Provider;

public class RoutePlanProvider
{
    private readonly FarmDbContext _db;
    public RoutePlanProvider(FarmDbContext db) => _db = db;

    public async Task<RoutePlan?> GetByIdAsync(int id) =>
        await _db.RoutePlans
            .Include(p => p.Routes)
            .ThenInclude(r => r.Stops)
            .Include(p => p.Routes)
            .ThenInclude(r => r.Vehicle)
            .ToListAsync()
            .ContinueWith(t => t.Result.FirstOrDefault(p => p.Id == id));

    public async Task<List<RoutePlan>> GetByDateAsync(DateOnly date) =>
        await _db.RoutePlans
            .Include(p => p.Routes)
            .Where(p => p.Date == date)
            .ToListAsync();

    public async Task AddAsync(RoutePlan entity)
    {
        await _db.RoutePlans.AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(RoutePlan entity)
    {
        _db.RoutePlans.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(RoutePlan entity)
    {
        _db.RoutePlans.Remove(entity);
        await _db.SaveChangesAsync();
    }
}