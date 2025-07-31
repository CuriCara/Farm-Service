using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class UnitsOfMeasurementProvider : IRepository<UnitsOfMeasurement>
{
    private readonly FarmDbContext _db;

    public UnitsOfMeasurementProvider(FarmDbContext db)
    {
        _db = db;
    }

    public async Task<List<UnitsOfMeasurement>> GetAllAsync() =>
        await _db.UnitsOfMeasurements.ToListAsync();

    public async Task<UnitsOfMeasurement> GetByIdAsync(int id) =>
        await _db.UnitsOfMeasurements.FindAsync(id);

    public async Task AddAsync(UnitsOfMeasurement entity)
    {
        _db.UnitsOfMeasurements.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(UnitsOfMeasurement entity)
    {
        _db.UnitsOfMeasurements.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(UnitsOfMeasurement entity)
    {
        _db.UnitsOfMeasurements.Remove(entity);
        await _db.SaveChangesAsync();
    }
}