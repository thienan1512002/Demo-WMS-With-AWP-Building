using DeliveryDemo.Application.Warehouse;

namespace DeliveryDemo.Api;

public static class MasterEndpoints
{
    public static void MapMasterEndpoints(this WebApplication app)
    {
        Map(app, "/api/goods", CatalogKind.Goods);
        Map(app, "/api/warehouses", CatalogKind.Warehouses);
    }

    private static void Map(WebApplication app, string path, CatalogKind kind)
    {
        var group = app.MapGroup(path);
        group.AddEndpointFilter(async (context, next) =>
        {
            if (context.HttpContext.RequestServices.GetService<IMasterCatalog>() is null)
                return Results.Problem(statusCode: 503, title: "Warehouse persistence is not configured.");
            try { return await next(context); }
            catch (CatalogConflictException e) { return Results.Problem(statusCode: 409, title: e.Message); }
        });
        group.MapGet("", async (int? page, int? pageSize, string? search, bool? archived, HttpContext context, CancellationToken ct) =>
        {
            var p = page ?? 1;
            var size = pageSize ?? 20;
            if (p < 1 || size < 1 || size > 100 || (long)(p - 1) * size > int.MaxValue)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["pagination"] = ["Page must be positive; pageSize must be between 1 and 100; offset must fit an integer."] });
            return Results.Ok(await Service(context).ListAsync(kind, p, size, search, archived, ct));
        });
        group.MapGet("/{id:guid}", async (Guid id, HttpContext context, CancellationToken ct) =>
            await Service(context).GetAsync(kind, id, ct) is { } item ? Results.Ok(item) : Results.NotFound());
        group.MapPost("", async (MasterInput input, HttpContext context, CancellationToken ct) =>
        {
            var errors = MasterValidation.Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var item = await Service(context).CreateAsync(kind, input, ct);
            return Results.Created($"{path}/{item.Id}", item);
        });
        group.MapPut("/{id:guid}", async (Guid id, MasterInput input, HttpContext context, CancellationToken ct) =>
        {
            var errors = MasterValidation.Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return await Service(context).UpdateAsync(kind, id, input, ct) is { } item ? Results.Ok(item) : Results.NotFound();
        });
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext context, CancellationToken ct) =>
            await Service(context).ArchiveAsync(kind, id, ct) ? Results.NoContent() : Results.NotFound());
    }

    private static IMasterCatalog Service(HttpContext context) => context.RequestServices.GetRequiredService<IMasterCatalog>();
}
