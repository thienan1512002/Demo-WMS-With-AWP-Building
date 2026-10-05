using System.Net;
using System.Net.Http.Json;
using DeliveryDemo.Application.Warehouse;
using DeliveryDemo.Infrastructure.Warehouse;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryDemo.Api.Tests;

public sealed class MasterApiTests
{
    [Theory]
    [InlineData("/api/goods")]
    [InlineData("/api/warehouses")]
    public async Task RoutesValidateAndTranslateServiceResults(string path)
    {
        var stub = new CatalogStub();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IMasterCatalog>(stub)));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new MasterInput(" ", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{path}/{stub.Item.Id}", new MasterInput("CODE", new string('n', 257)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?page=2147483647&pageSize=100")).StatusCode);
        Assert.Equal(0, stub.Calls);
        var created = await client.PostAsJsonAsync(path, new MasterInput("CODE", "Name"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(path + "/" + stub.Item.Id, created.Headers.Location!.OriginalString);
        Assert.Equal(stub.Item, await client.GetFromJsonAsync<MasterDetail>(created.Headers.Location));
        var page = await client.GetFromJsonAsync<MasterPage>(path + "?page=2&pageSize=5&search=Name&archived=false");
        Assert.Equal(2, page!.Page);
        Assert.Equal(5, page.PageSize);
        Assert.Equal("Name", stub.Search);
        Assert.False(stub.Archived);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(created.Headers.Location, new MasterInput("CODE", "Updated"))).StatusCode);
        stub.Conflict = true;
        var conflict = await client.PostAsJsonAsync(path, new MasterInput("CODE", "Name"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType!.MediaType);
        var updateConflict = await client.PutAsJsonAsync(created.Headers.Location, new MasterInput("CODE", "Updated"));
        Assert.Equal(HttpStatusCode.Conflict, updateConflict.StatusCode);
        Assert.Equal("application/problem+json", updateConflict.Content.Headers.ContentType!.MediaType);
        stub.Conflict = false;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
        var missing = path + "/" + Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(missing)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(missing, new MasterInput("CODE", "Name"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(missing)).StatusCode);
    }

    [Theory]
    [InlineData("/api/goods")]
    [InlineData("/api/warehouses")]
    public async Task UnconfiguredPersistenceReturnsServiceUnavailable(string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData(null, "Name")]
    [InlineData("CODE", " ")]
    [InlineData("", "Name")]
    public async Task ServiceRejectsInvalidDataBeforeDatabaseAccess(string? code, string? name)
    {
        await using var db = new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>().UseNpgsql().Options);
        var service = new MasterCatalog(db);
        foreach (var kind in Enum.GetValues<CatalogKind>())
        {
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(kind, new(code, name), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(kind, Guid.NewGuid(), new(code, name), default));
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("/api/goods", 64, 256, true)]
    [InlineData("/api/warehouses", 64, 256, true)]
    [InlineData("/api/goods", 65, 256, false)]
    [InlineData("/api/warehouses", 65, 256, false)]
    [InlineData("/api/goods", 64, 257, false)]
    [InlineData("/api/warehouses", 64, 257, false)]
    public async Task CodeAndNameLengthLimitsApplyAfterTrimming(string path, int codeLength, int nameLength, bool valid)
    {
        var stub = new CatalogStub();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IMasterCatalog>(stub)));
        using var client = factory.CreateClient();
        var input = new MasterInput(" " + new string('c', codeLength) + " ", " " + new string('n', nameLength) + " ");
        var response = await client.PostAsJsonAsync(path, input);
        Assert.Equal(valid ? HttpStatusCode.Created : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(valid ? 1 : 0, stub.Calls);
        if (!valid)
        {
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
            Assert.Contains(codeLength > 64 ? "code" : "name", problem!.Errors.Keys);
        }
    }

    private sealed class CatalogStub : IMasterCatalog
    {
        public MasterDetail Item { get; } = new(Guid.NewGuid(), "CODE", "Name", DateTime.UtcNow, null);
        public int Calls { get; private set; }
        public bool Conflict { get; set; }
        public string? Search { get; private set; }
        public bool? Archived { get; private set; }
        public Task<MasterPage> ListAsync(CatalogKind kind, int page, int pageSize, string? search, bool? archived, CancellationToken cancellationToken)
        {
            Calls++;
            Search = search;
            Archived = archived;
            return Task.FromResult(new MasterPage([Item], 1, page, pageSize));
        }
        public Task<MasterDetail?> GetAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken) => Task.FromResult(id == Item.Id ? Item : null);
        public Task<MasterDetail> CreateAsync(CatalogKind kind, MasterInput input, CancellationToken cancellationToken)
        {
            Calls++;
            if (Conflict) throw new CatalogConflictException("Duplicate code.");
            return Task.FromResult(Item);
        }
        public Task<MasterDetail?> UpdateAsync(CatalogKind kind, Guid id, MasterInput input, CancellationToken cancellationToken)
        {
            Calls++;
            if (Conflict) throw new CatalogConflictException("Archived or immutable code.");
            return Task.FromResult(id == Item.Id ? Item : null);
        }
        public Task<bool> ArchiveAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken) => Task.FromResult(id == Item.Id);
    }
}
