using System.Net;
using System.Net.Http.Json;
using JiggerJot.Api.Tests.Infrastructure;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// AUTHORING-1 over real HTTP: routing and model binding, which the handler tests cannot see.
/// <para>
/// This file exists because the browser found a failure that twenty handler tests passed straight
/// through. The form sends <c>servingType: "FullDrink"</c> and <c>role: "Base"</c> — the names, as
/// any client would — and the request could not be bound at all, because nothing had ever sent this
/// API an enum before. Every response until now turned enums into strings on the way out
/// (<c>ServingType.ToString()</c>), so the wire had only ever carried them one way.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class CocktailAuthoringEndpointTests(IntegrationTestFactory factory)
{
    private record Option(Guid Id, string Name);
    private record Lookups(List<Option> Glasses, List<Option> Methods, List<Option> Units,
        List<string> ServingTypes, List<string> Roles);
    private record ShelfRow(Guid Id, string Name, string Category, string? Subcategory, bool IsAvailable, bool IsOwn);
    private record Created(Guid Id);
    private record LineRow(string Ingredient, string Display, bool IsRequired, string Role);
    private record Detail(Guid Id, string Name, string? Glass, string? Method, string ServingType,
        List<LineRow> Lines);

    [Fact]
    public async Task WritingOne_OverHttp_TakesTheEnumsByName()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);

        var lookups = await client.GetFromJsonAsync<Lookups>("/api/cocktails/lookups");
        var shelf = await client.GetFromJsonAsync<List<ShelfRow>>("/api/inventory");
        var ml = lookups!.Units.Single(u => u.Name == "ml");
        var gin = shelf!.First(i => i.Name == "London dry gin");

        var response = await client.PostAsJsonAsync("/api/cocktails", new
        {
            name = "Endpoint Special",
            glassTypeId = (Guid?)null,
            methodId = (Guid?)null,
            // By NAME, the way a client sends an enum it read from /lookups — which returns names.
            servingType = "FullDrink",
            instructions = "Stir.",
            lines = new[]
            {
                new { ingredientId = gin.Id, amount = 30m, unitId = ml.Id, isRequired = true, role = "Base", notes = (string?)null },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<Created>();
        var detail = await client.GetFromJsonAsync<Detail>($"/api/cocktails/{created!.Id}");

        Assert.Equal("Endpoint Special", detail!.Name);
        Assert.Equal("FullDrink", detail.ServingType);
        var line = Assert.Single(detail.Lines);
        Assert.Equal("Base", line.Role);
        Assert.Equal("London dry gin", line.Ingredient);
    }

    [Fact]
    public async Task ABadRequest_OverHttp_ComesBackAsACodeTheFormCanRead()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);

        var response = await client.PostAsJsonAsync("/api/cocktails", new
        {
            name = "   ",
            glassTypeId = (Guid?)null,
            methodId = (Guid?)null,
            servingType = "FullDrink",
            instructions = (string?)null,
            lines = Array.Empty<object>(),
        });

        // 400 with a code, not a binding failure: the form maps each code to a sentence someone can
        // act on, and a generic failure would leave it saying "try again" to a fixable mistake.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("invalid_name", problem!["error"]);
    }
}
