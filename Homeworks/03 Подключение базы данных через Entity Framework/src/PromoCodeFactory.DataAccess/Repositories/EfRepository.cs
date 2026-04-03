using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain;
using PromoCodeFactory.Core.Exceptions;

namespace PromoCodeFactory.DataAccess.Repositories;

internal class EfRepository<T>(PromoCodeFactoryDbContext context) : IRepository<T> where T : BaseEntity
{
    protected PromoCodeFactoryDbContext Context => context;
    protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query) => query;

    public async Task Add(T entity, CancellationToken ct)
    {
        await Context.Set<T>().AddAsync(entity, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task Delete(Guid id, CancellationToken ct)
    {
        var entity = await GetById(id, false, ct);
        if (entity is null)
            throw new EntityNotFoundException<T>(id);
        Context.Set<T>().Remove(entity);
        await Context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<T>> GetAll(bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = Context.Set<T>();
        if (withIncludes)
            query = ApplyIncludes(query);
        return await query.ToListAsync(ct);
    }

    public async Task<T?> GetById(Guid id, bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = Context.Set<T>();
        if (withIncludes)
            query = ApplyIncludes(query);
        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyCollection<T>> GetByRangeId(IEnumerable<Guid> ids, bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = Context.Set<T>();
        if (withIncludes)
            query = ApplyIncludes(query);
        return await query.Where(e => ids.Contains(e.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<T>> GetWhere(Expression<Func<T, bool>> predicate, bool withIncludes = false, CancellationToken ct = default)
    {
        IQueryable<T> query = Context.Set<T>();
        if (withIncludes)
            query = ApplyIncludes(query);
        return await query.Where(predicate).ToListAsync(ct);
    }

    public async Task Update(T entity, CancellationToken ct)
    {
        if (!await Context.Set<T>().AnyAsync(e => e.Id == entity.Id, ct))
            throw new EntityNotFoundException<T>(entity.Id);
        Context.Set<T>().Update(entity);
        await Context.SaveChangesAsync(ct);
    }
}
