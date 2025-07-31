using DataAccess.Entity;

namespace BusinessLogic.Harvests.Provider;

public interface IRepository<T> where T : class, IBaseEntity
{
    Task<List<T>> GetAllAsync();
    Task<T> GetByIdAsync(int id);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
