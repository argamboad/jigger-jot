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

    private record ShelfRow(Guid Id, string Name, string Category, string? Subcategory, bool IsAvailable, bool IsOwn);
}
