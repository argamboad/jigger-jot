using System.Net;
using System.Net.Http.Json;
using JiggerJot.Api.Tests.Infrastructure;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// AUTHORING-5 and INV-4 over real HTTP: the two DELETE routes, and the status each refusal becomes —
/// which the handler tests cannot see.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class DeletingEndpointTests(IntegrationTestFactory factory)
{
    private record ShelfRow(Guid Id, string Name, string Category, string? Subcategory, bool IsAvailable, bool IsOwn);
    private record Category(Guid Id, string Name, List<Category> Subcategories);
    private record Created(Guid Id);
    private record Page(List<Row> Items);
    private record Row(Guid Id, string Name, bool IsOwn);
    private record InUse(string Error, string Message, List<string> UsedIn);

    private static async Task<Guid> WriteAsync(HttpClient client, string name, Guid ingredient)
    {
        var response = await client.PostAsJsonAsync("/api/cocktails", new
        {
            name,
            glassTypeId = (Guid?)null,
            methodId = (Guid?)null,
            servingType = "FullDrink",
            instructions = (string?)null,
            lines = new[] { new { ingredientId = ingredient, amount = (decimal?)null, unitId = (Guid?)null, isRequired = true, role = "Base", notes = (string?)null } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    [Fact]
    public async Task DeletingACocktail_OverHttp_Is204_403ForTheBook_404Afterwards()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);
        var shelf = await client.GetFromJsonAsync<List<ShelfRow>>("/api/inventory");
        var mine = await WriteAsync(client, "Delete Me Sour", shelf!.First(i => i.Name == "London dry gin").Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/cocktails/{mine}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/cocktails/{mine}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/cocktails/{mine}")).StatusCode);

        var book = (await client.GetFromJsonAsync<Page>("/api/cocktails?search=negroni"))!.Items.First(r => !r.IsOwn);
        var refused = await client.DeleteAsync($"/api/cocktails/{book.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("catalog_read_only", (await refused.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["error"]);
    }

    [Fact]
    public async Task DeletingABottle_OverHttp_Is409WhileARecipeUsesIt_Then204()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);
        var categories = await client.GetFromJsonAsync<List<Category>>("/api/inventory/categories");

        var added = await client.PostAsJsonAsync("/api/inventory/ingredients",
            new { name = "Endpoint orgeat", categoryId = categories![0].Id, subcategoryId = (Guid?)null });
        var bottle = (await added.Content.ReadFromJsonAsync<ShelfRow>())!.Id;
        var recipe = await WriteAsync(client, "Orgeat Fizz", bottle);

        var refused = await client.DeleteAsync($"/api/inventory/ingredients/{bottle}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var body = await refused.Content.ReadFromJsonAsync<InUse>();
        Assert.Equal("ingredient_in_use", body!.Error);
        Assert.Equal(["Orgeat Fizz"], body.UsedIn);

        await client.DeleteAsync($"/api/cocktails/{recipe}");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/inventory/ingredients/{bottle}")).StatusCode);

        var shared = (await client.GetFromJsonAsync<List<ShelfRow>>("/api/inventory"))!.First(i => !i.IsOwn);
        var readOnly = await client.DeleteAsync($"/api/inventory/ingredients/{shared.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
        Assert.Equal("catalog_read_only", (await readOnly.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["error"]);
    }
}
