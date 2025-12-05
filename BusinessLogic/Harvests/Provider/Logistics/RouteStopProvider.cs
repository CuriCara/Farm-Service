using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Logistics.Provider;

public class RouteStopProvider
{
    private readonly FarmDbContext _db;
    public RouteStopProvider(FarmDbContext db) => _db = db;

    public async Task<List<RouteStop>> GetByRouteAsync(int routeId) =>
        await _db.RouteStops
            .Where(s => s.RouteId == routeId)
            .OrderBy(s => s.StopIndex)
            .ToListAsync();

    public async Task AddAsync(RouteStop entity)
    {
        await _db.RouteStops.AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(RouteStop entity)
    {
        _db.RouteStops.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(RouteStop entity)
    {
        _db.RouteStops.Remove(entity);
        await _db.SaveChangesAsync();
    }
}