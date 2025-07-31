using BusinessLogic.Harvests.Provider;
using DataAccess.Entity;

namespace BusinessLogic.Harvests.Manager;

public class ProductManager
{
    private readonly IRepository<Product> _repo;

    public ProductManager(IRepository<Product> repo)
    {
        _repo = repo;
    }

    public Task<List<Product>> GetAllAsync => _repo.GetAllAsync();
    public Task<Product> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
    public Task AddAsync(Product product) => _repo.AddAsync(product);
    public Task UpdateAsync(Product product) => _repo.UpdateAsync(product);
    public Task DeleteAsync(Product product) => _repo.DeleteAsync(product);
}