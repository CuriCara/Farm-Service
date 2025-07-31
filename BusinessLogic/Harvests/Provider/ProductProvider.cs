using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class ProductProvider : IRepository<Product>
{
    private readonly FarmDbContext _db;
    public ProductProvider(FarmDbContext db) { _db = db; }

    public async Task<List<Product>> GetAllAsync() =>
        await _db.Products.ToListAsync();
    public async Task<Product> GetByIdAsync(int id) =>
        await _db.Products.FindAsync(id);
    public async Task AddAsync(Product entity)
    {
        _db.Products.Add(entity);
        await _db.SaveChangesAsync();
    }
    public async Task UpdateAsync(Product entity)
    {
        _db.Products.Update(entity);
        await _db.SaveChangesAsync();
    }
    public async Task DeleteAsync(Product entity)
    {
        _db.Products.Remove(entity);
        await _db.SaveChangesAsync();
    }
}