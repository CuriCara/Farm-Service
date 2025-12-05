using BusinessLogic.Harvests.Provider;
using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Manager;

public class HarvestManager
{
    private readonly IRepository<Harvest> _repo;
    private readonly FarmDbContext _db;

    public HarvestManager(IRepository<Harvest> repo, FarmDbContext db)
    {
        _repo = repo;
        _db = db;
    }

    public async Task<List<Harvest>> GetAllAsync()
    {
        return await _db.Harvests
            .Include(h => h.Unit)
                .ThenInclude(u => u.Category)
                    .ThenInclude(c => c.BaseUnit)
            .Include(h => h.Product)
            .Include(h => h.User)
            .Include(h => h.Farm)
            .ToListAsync();
    }
    public Task<Harvest> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
    public Task AddAsync(Harvest harvest) => _repo.AddAsync(harvest);
    public Task UpdateAsync(Harvest harvest) => _repo.UpdateAsync(harvest);
    public Task DeleteAsync(Harvest harvest) => _repo.DeleteAsync(harvest);
}