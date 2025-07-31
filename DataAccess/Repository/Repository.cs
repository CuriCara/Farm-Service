using System.Linq.Expressions;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repository;

public class Repository<T> : IRepository<T> where T : class, IBaseEntity
{
    private FarmDbContext _context;

    public Repository(FarmDbContext context)
    {
        _context = context;
    }
    
    public IQueryable<T> GetAll()
    {
        return _context.Set<T>();
    }
    
    public IQueryable<T> GetAllAsync()
    {
        return _context.Set<T>();
    }

    
    public IQueryable<T> GetAll(Expression<Func<T, bool>> predicate)
    {
        return _context.Set<T>().Where(predicate);
    }

    public T? GetById(int id)
    {
        return _context.Set<T>().FirstOrDefault(x => x.Id == id);
    }

    public T Save(T entity)
    {
        if (entity.CreationTime == entity.ModificationTime)
        {
            entity.ModificationTime = DateTime.UtcNow;
            var result = _context.Set<T>().Add(entity);
            _context.SaveChanges();
            return result.Entity;
        }
        else
        {
            entity.ExternalId = Guid.NewGuid();
            entity.CreationTime = DateTime.UtcNow;
            entity.ModificationTime = DateTime.UtcNow;
            var result = _context.Set<T>().Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            _context.SaveChanges();
            return result.Entity;
        }
    }

    public void Delete(T entity)
    {
        _context.Set<T>().Attach(entity);
        _context.Entry(entity).State = EntityState.Deleted;
        _context.SaveChanges();
    }
}



// using Microsoft.EntityFrameworkCore;
// using BusinessLogic.Harvests.Provider;
// using DataAccess;
// using DataAccess.Entity;
//
// namespace DataAccess.Repository
// {
//     public class Repository<T> : IRepository<T> where T : class,IBaseEntity
//     {
//         private readonly FarmDbContext _context;
//
//         public Repository(FarmDbContext context)
//         {
//             _context = context ?? throw new ArgumentNullException(nameof(context));
//         }
//
//         public async Task<List<T>> GetAllAsync()
//         {
//             return await _context.Set<T>().ToListAsync();
//         }
//
//         public async Task<T> GetByIdAsync(int id)
//         {
//             return await _context.Set<T>().FindAsync(id);
//         }
//
//         public async Task AddAsync(T entity)
//         {
//             await _context.Set<T>().AddAsync(entity);
//             await _context.SaveChangesAsync();
//         }
//
//         public async Task UpdateAsync(T entity)
//         {
//             _context.Set<T>().Update(entity);
//             await _context.SaveChangesAsync();
//         }
//
//         public async Task DeleteAsync(T entity)
//         {
//             _context.Set<T>().Remove(entity);
//             await _context.SaveChangesAsync();
//         }
//     }
// }