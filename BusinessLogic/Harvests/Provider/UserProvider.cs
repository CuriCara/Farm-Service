using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Harvests.Provider;

public class UserProvider : IRepository<User>
{
    private readonly FarmDbContext _db;

    public UserProvider(FarmDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<List<User>> GetAllAsync() =>
        await _db.Users.ToListAsync();

    public async Task<User> GetByIdAsync(int id) =>
        await _db.Users.FindAsync(id);

    public async Task AddAsync(User entity)
    {
        _db.Users.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(User entity)
    {
        _db.Users.Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(User entity)
    {
        _db.Users.Remove(entity);
        await _db.SaveChangesAsync();
    }
}