namespace OpsDash.Domain.Interfaces;

public interface IRepository<T>
    where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetByIdAsync(long id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);
    void Delete(T entity);
    Task<int> SaveChangesAsync();
}
