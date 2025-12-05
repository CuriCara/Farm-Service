using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Logistics.Provider;

public class RouteProvider
{
    private readonly FarmDbContext _db;
    public RouteProvider(FarmDbContext db) => _db = db;

    public async Task<List<Route>> GetByPlanAsync(int planId) =>
        await _db.Routes
            .Include(r => r.Stops)
            .Include(r => r.Vehicle)
            .Where(r => r.RoutePlanId == planId)
            .ToListAsync();

    public async Task AddAsync(Route entity)
    {
        await _db.Routes.AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Route entity)
    {
        _db.Routes.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Route entity)
    {
        _db.Routes.Remove(entity);
        await _db.SaveChangesAsync();
    }
}