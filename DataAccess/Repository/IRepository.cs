using System.Linq.Expressions;

namespace DataAccess.Repository;

using DataAccess.Entity;

public interface IRepository<T> where T : class,IBaseEntity
{
        IQueryable<T> GetAll();
        IQueryable<T> GetAllAsync();
        
        IQueryable<T> GetAll(Expression<Func<T, bool>> predicate);

        T? GetById(int id);

        T Save(T entity);

        void Delete(T entity);
    
}