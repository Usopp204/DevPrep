using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;

namespace PaymentAPI.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly PaymentDbContext _db;
    protected readonly DbSet<T> _set;

    protected Repository(PaymentDbContext db)
    {
        _db  = db;
        _set = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id)        => await _set.FindAsync(id);
    public async Task<IEnumerable<T>> GetAllAsync()     => await _set.ToListAsync();
    public async Task AddAsync(T entity)                { await _set.AddAsync(entity); }
    public void Update(T entity)                        => _set.Update(entity);
    public void Remove(T entity)                        => _set.Remove(entity);
    public async Task<int> SaveChangesAsync()           => await _db.SaveChangesAsync();
}
