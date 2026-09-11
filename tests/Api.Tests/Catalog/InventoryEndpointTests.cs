using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// INV-1 over real HTTP: routing, model binding and auth, which the handler tests cannot see. The
/// browser journey found a failure the handler tests all passed through, so this is the layer that
/// was missing.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class InventoryEndpointTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Tick_OverHttp_Returns204_AndShowsUpOnTheShelf()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);

        var shelf = await client.GetFromJsonAsync<List<ShelfRow>>("/api/inventory");
        Assert.NotNull(shelf);
        Assert.NotEmpty(shelf!);

        var first = shelf![0];
        var response = await client.PutAsJsonAsync($"/api/inventory/{first.Id}", new { isAvailable = true });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await client.GetFromJsonAsync<List<ShelfRow>>("/api/inventory");
        Assert.True(after!.Single(i => i.Id == first.Id).IsAvailable);
    }

    [Fact]
    public async Task TheWizardsWrite_BindsItsList_AndFillsTheShelf()
    {
        // ONBOARD-1. The handler tests cover what it does; this covers whether the request reaches it
        // at all. The body is a nested LIST, which is the shape nothing on this API had sent before —
        // and the last time a request carried a shape new to the app (AUTHORING-1's enums) twenty
        // green handler tests sat above an endpoint that could not bind it.
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);

        var shelf = await client.GetFromJsonAsync<List<ShelfRow>>("/api/inventory");
        var picked = shelf!.Take(3).Select(i => i.Id).ToList();

        var response = await client.PutAsJsonAsync("/api/inventory", new
        {
            items = picked.Select(id => new { ingredientId = id, isAvailable = true }),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkResult>();
        Assert.Equal(3, result!.Applied);
        Assert.Empty(result.Unknown);

        var after = await client.GetFromJsonAsync<List<ShelfRow>>("/api/inventory");
        Assert.Equal([.. picked.Order()], [.. after!.Where(i => i.IsAvailable).Select(i => i.Id).Order()]);
    }

    private record ShelfRow(Guid Id, string Name, string Category, string? Subcategory, bool IsAvailable, bool IsOwn);

    private record BulkResult(int Applied, IReadOnlyList<Guid> Unknown);
}
