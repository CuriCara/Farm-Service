using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class StoreProvider : IRepository<Store>
{
    private readonly FarmDbContext _db;

    public StoreProvider(FarmDbContext db)
    {
        _db = db;
    }

    public async Task<List<Store>> GetAllAsync() =>
        await _db.Stores
            .Include(s => s.Products)
            .ThenInclude(sp => sp.Product)
            .ToListAsync();
    public async Task<Store> GetByIdAsync(int id) =>
        await _db.Stores
            .Include(s => s.Products)
            .ThenInclude(sp => sp.Product)
            .FirstOrDefaultAsync(s => s.Id == id);
    public async Task AddAsync(Store entity)
    {
        _db.Stores.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Store entity)
    {
        _db.Stores.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Store entity)
    {
        _db.Stores.Remove(entity);
        await _db.SaveChangesAsync();
    }
}