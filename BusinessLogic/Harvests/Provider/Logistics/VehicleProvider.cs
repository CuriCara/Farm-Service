using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Logistics.Provider;

public class VehicleProvider
{
    private readonly FarmDbContext _db;
    public VehicleProvider(FarmDbContext db) => _db = db;

    public async Task<List<Vehicle>> GetAllAsync() =>
        await _db.Vehicles.ToListAsync();

    public async Task<Vehicle?> GetByIdAsync(int id) =>
        await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id);

    public async Task AddAsync(Vehicle entity)
    {
        await _db.Vehicles.AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Vehicle entity)
    {
        _db.Vehicles.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Vehicle entity)
    {
        _db.Vehicles.Remove(entity);
        await _db.SaveChangesAsync();
    }
}