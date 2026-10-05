using DeliveryDemo.Application.Warehouse;
using DeliveryDemo.Domain.Warehouse;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeliveryDemo.Infrastructure.Warehouse;

public sealed class MasterCatalog(WarehouseDbContext db) : IMasterCatalog
{
    private IQueryable<MasterDetail> Query(CatalogKind kind) => kind == CatalogKind.Goods
        ? db.Goods.AsNoTracking().Select(x => new MasterDetail { Id = x.Id, Code = x.Code, Name = x.Name, CreatedAt = x.CreatedAt, ArchivedAt = x.ArchivedAt })
        : db.Warehouses.AsNoTracking().Select(x => new MasterDetail { Id = x.Id, Code = x.Code, Name = x.Name, CreatedAt = x.CreatedAt, ArchivedAt = x.ArchivedAt });

    public async Task<MasterPage> ListAsync(CatalogKind kind, int page, int pageSize, string? search, bool? archived, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ArgumentException("Invalid pagination.");
        var query = Query(kind);
        if (archived.HasValue) query = query.Where(x => (x.ArchivedAt != null) == archived.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip(checked((page - 1) * pageSize)).Take(pageSize).ToListAsync(cancellationToken);
        return new(items, total, page, pageSize);
    }

    public Task<MasterDetail?> GetAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken) =>
        Query(kind).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<MasterDetail> CreateAsync(CatalogKind kind, MasterInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var code = input.Code!.Trim();
        if (await Query(kind).AnyAsync(x => x.Code == code, cancellationToken))
            throw new CatalogConflictException("Code is already reserved, including archived records.");
        object entity = kind == CatalogKind.Goods
            ? new Goods { Code = code, Name = input.Name!.Trim() }
            : new Domain.Warehouse.Warehouse { Code = code, Name = input.Name!.Trim() };
        db.Add(entity);
        await SaveAsync(cancellationToken);
        // Return the persisted values, including PostgreSQL's microsecond timestamp
        // precision, so create and subsequent reads agree.
        await db.Entry(entity).ReloadAsync(cancellationToken);
        return Detail(entity);
    }

    public async Task<MasterDetail?> UpdateAsync(CatalogKind kind, Guid id, MasterInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var entity = await FindAsync(kind, id, cancellationToken);
        if (entity is null) return null;
        var current = Detail(entity);
        if (current.Code != input.Code!.Trim()) throw new CatalogConflictException("Code cannot be changed after creation.");
        if (current.ArchivedAt != null) throw new CatalogConflictException("Archived records cannot be updated.");
        db.Entry(entity).Property("Name").CurrentValue = input.Name!.Trim();
        await SaveAsync(cancellationToken);
        return Detail(entity);
    }

    public async Task<bool> ArchiveAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(kind, id, cancellationToken);
        if (entity is null) return false;
        if (Detail(entity).ArchivedAt is null)
        {
            db.Entry(entity).Property("ArchivedAt").CurrentValue = DateTime.UtcNow;
            await SaveAsync(cancellationToken);
        }
        return true;
    }

    private async Task<object?> FindAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken) => kind == CatalogKind.Goods
        ? await db.Goods.FindAsync([id], cancellationToken)
        : await db.Warehouses.FindAsync([id], cancellationToken);

    private static MasterDetail Detail(object entity) => entity switch
    {
        Goods x => new(x.Id, x.Code, x.Name, x.CreatedAt, x.ArchivedAt),
        Domain.Warehouse.Warehouse x => new(x.Id, x.Code, x.Name, x.CreatedAt, x.ArchivedAt),
        _ => throw new ArgumentException("Unknown master entity.")
    };

    private static void Validate(MasterInput input)
    {
        if (MasterValidation.Validate(input).Count > 0) throw new ArgumentException("Invalid master data.");
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            throw new CatalogConflictException("Code is already reserved, including archived records.");
        }
    }
}
