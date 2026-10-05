namespace DeliveryDemo.Application.Warehouse;

public enum CatalogKind { Goods, Warehouses }
public sealed record MasterInput(string? Code, string? Name);
public sealed record MasterDetail(Guid Id, string Code, string Name, DateTime CreatedAt, DateTime? ArchivedAt)
{
    public MasterDetail() : this(Guid.Empty, "", "", default, null) { }
}
public sealed record MasterPage(IReadOnlyList<MasterDetail> Items, int Total, int Page, int PageSize);
public sealed class CatalogConflictException(string message) : Exception(message);

public static class MasterValidation
{
    public static Dictionary<string, string[]> Validate(MasterInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Trim().Length > 64)
            errors["code"] = ["Code is required and must contain at most 64 characters."];
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 256)
            errors["name"] = ["Name is required and must contain at most 256 characters."];
        return errors;
    }
}

public interface IMasterCatalog
{
    Task<MasterPage> ListAsync(CatalogKind kind, int page, int pageSize, string? search, bool? archived, CancellationToken cancellationToken);
    Task<MasterDetail?> GetAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken);
    Task<MasterDetail> CreateAsync(CatalogKind kind, MasterInput input, CancellationToken cancellationToken);
    Task<MasterDetail?> UpdateAsync(CatalogKind kind, Guid id, MasterInput input, CancellationToken cancellationToken);
    Task<bool> ArchiveAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken);
}
