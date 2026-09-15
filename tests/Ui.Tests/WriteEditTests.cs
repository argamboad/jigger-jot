using System.Net;
using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// AUTHORING-2: the write form editing a cocktail the household owns — the same page as writing one,
/// opened from the draft, saved with a PUT. And the recipe page offers Edit only where editing is
/// allowed.
/// </summary>
public class WriteEditTests : ComponentTestBase
{
    private static readonly Guid Id = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private const string Coupe = "11111111-1111-1111-1111-111111111111";
    private const string Ml = "33333333-3333-3333-3333-333333333333";
    private const string Gin = "44444444-4444-4444-4444-444444444444";
    private const string Lemon = "55555555-5555-5555-5555-555555555555";

    private const string Lookups = $$"""
        {"glasses":[{"id":"{{Coupe}}","name":"Coupe"}],
         "methods":[{"id":"22222222-2222-2222-2222-222222222222","name":"Shake"}],
         "units":[{"id":"{{Ml}}","name":"ml"}],
         "servingTypes":["FullDrink","Shot"],
         "roles":["Base","Modifier","Juice","Syrup","Bitters","Garnish","Mixer","Other"]}
        """;

    private const string Inventory = $$"""
        [{"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":true,"isOwn":false},
         {"id":"{{Lemon}}","name":"Lemon juice","category":"Juice","subcategory":null,"isAvailable":true,"isOwn":false}]
        """;

    // A metric writer's draft: the stored 1 1/2 oz and 3/4 oz come back as millilitres. The lemon is
    // deliberately a Modifier, so a suggestion that overwrote it would show.
    private static readonly string Draft = $$"""
        {"id":"{{Id}}","name":"House Sour","glassTypeId":"{{Coupe}}","methodId":null,"servingType":"FullDrink",
         "instructions":"Shake hard.",
         "lines":[{"ingredientId":"{{Gin}}","amount":45,"unitId":"{{Ml}}","isRequired":true,"role":"Base","notes":null},
                  {"ingredientId":"{{Lemon}}","amount":22.5,"unitId":"{{Ml}}","isRequired":true,"role":"Modifier","notes":null}]}
        """;

    private IRenderedComponent<WriteCocktail> RenderEdit(string? draft = null, HttpStatusCode draftStatus = HttpStatusCode.OK)
    {
        Http.On(HttpMethod.Get, "/api/cocktails/lookups", Lookups);
        Http.On(HttpMethod.Get, "/api/inventory", Inventory);
        Http.On(HttpMethod.Get, $"/api/cocktails/{Id}/draft", draft ?? Draft, draftStatus);
        Http.On(HttpMethod.Get, "/api/cocktails/roles", $$"""
            [{"ingredientId":"{{Lemon}}","role":"Juice","isRequired":true},
             {"ingredientId":"{{Lemon}}","role":"Juice","isRequired":true}]
            """);
        Http.On(HttpMethod.Put, $"/api/cocktails/{Id}", $$"""{"id":"{{Id}}"}""");
        return Render<WriteCocktail>(ps => ps.Add(p => p.Id, Id));
    }

    private static List<string?> Values(IRenderedComponent<WriteCocktail> page, string testId) =>
        [.. page.FindAll($"[data-testid='{testId}']").Select(e => e.GetAttribute("value"))];

    [Fact]
    public void TheFormOpensFilledIn_InTheWritersUnits_WithTheRolesTheRecipeHas()
    {
        var page = RenderEdit();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-testid='new-line']").Count));

        Assert.Contains("Cocktail_Edit", page.Find("h1.page-title").TextContent);
        Assert.Equal("House Sour", page.Find("[data-testid='new-name']").GetAttribute("value"));
        Assert.Equal("Shake hard.", page.Find("[data-testid='new-instructions']").GetAttribute("value"));
        Assert.Equal(Coupe, page.Find("[data-testid='new-glass']").GetAttribute("value"));

        Assert.Equal(["London dry gin", "Lemon juice"], Values(page, "new-line-ingredient"));
        Assert.Equal(["45", "22.5"], Values(page, "new-line-amount"));
        Assert.Equal([Ml, Ml], Values(page, "new-line-unit"));
        Assert.Equal(["Base", "Modifier"], Values(page, "new-line-role"));
    }

    [Fact]
    public async Task SavingSendsAPut_AndReturnsToTheRecipe()
    {
        var page = RenderEdit();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-testid='new-line']").Count));

        page.Find("[data-testid='new-name']").Change("House Sour, Stronger");
        await page.Find("[data-testid='new-save']").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            var put = Http.Requests.FindIndex(r => r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == $"/api/cocktails/{Id}");
            Assert.True(put >= 0, "the edit is a PUT to the cocktail itself");
            Assert.Contains("House Sour, Stronger", Http.Bodies[put]);
            Assert.EndsWith($"/cocktails/{Id}", Services.GetRequiredService<NavigationManager>().Uri);
        });
        Assert.DoesNotContain(Http.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public void TheRolesTheRecipeAlreadyHas_AreNeverOverwrittenBySuggestions()
    {
        var page = RenderEdit();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-testid='new-line']").Count));

        // Re-pick the first line's bottle, which asks for suggestions for the whole recipe.
        page.FindAll("[data-testid='new-line-ingredient']")[0].Input("Lemon juice");
        page.Find("[data-testid='new-line-ingredient-option']").MouseDown();

        page.WaitForAssertion(() => Assert.Contains(Http.Requests, r => r.RequestUri!.AbsolutePath == "/api/cocktails/roles"));
        // Both lines came from the recipe, so both count as set by hand (AUTHORING-3 never wins over them).
        Assert.Equal(["Base", "Modifier"], Values(page, "new-line-role"));
    }

    [Fact]
    public void ASharedCatalogRecipe_SaysItCannotBeEdited_AndShowsNoForm()
    {
        var page = RenderEdit("""{"error":"catalog_read_only","message":"read only"}""", HttpStatusCode.Forbidden);

        page.WaitForAssertion(() =>
            Assert.Contains("Cocktail_ErrReadOnly", page.Find("[data-testid='edit-read-only']").TextContent));
        Assert.Empty(page.FindAll("[data-testid='new-line']"));
        Assert.Equal($"/cocktails/{Id}", page.Find("[data-testid='edit-read-only'] a").GetAttribute("href"));
    }

    [Fact]
    public void WritingANewOne_IsUnchanged_ByTheEditRoute()
    {
        Http.On(HttpMethod.Get, "/api/cocktails/lookups", Lookups);
        Http.On(HttpMethod.Get, "/api/inventory", Inventory);

        var page = Render<WriteCocktail>();

        page.WaitForAssertion(() => page.Find("[data-testid='new-name']"));
        Assert.Contains("Cocktail_New", page.Find("h1.page-title").TextContent);
        Assert.DoesNotContain(Http.Requests, r => r.RequestUri!.AbsolutePath.EndsWith("/draft"));
    }

    // ── the recipe page ──────────────────────────────────────────────────────

    private static string Recipe(bool isOwn) => $$"""
        {"id":"{{Id}}","name":"House Sour","glass":null,"method":null,"servingType":"FullDrink",
         "instructions":null,"source":null,"isOwn":{{(isOwn ? "true" : "false")}},"makeability":"Makeable",
         "forkedFrom":null,
         "lines":[{"ingredient":"London dry gin","amount":1.5,"unit":"oz","display":"1 1/2 oz","isRequired":true,
                   "role":"Base","notes":null,"availability":"Available","substituteWith":null}]}
        """;

    [Fact]
    public void TheRecipePage_OffersEdit_OnlyOnTheHouseholdsOwnCocktails()
    {
        Http.On(HttpMethod.Get, $"/api/cocktails/{Id}", Recipe(isOwn: true));
        var own = Render<CocktailDetail>(ps => ps.Add(p => p.Id, Id));
        own.WaitForAssertion(() =>
            Assert.Equal($"/cocktails/{Id}/edit", own.Find("[data-testid='cocktail-edit']").GetAttribute("href")));

        Http.On(HttpMethod.Get, $"/api/cocktails/{Id}", Recipe(isOwn: false));
        var shared = Render<CocktailDetail>(ps => ps.Add(p => p.Id, Id));
        shared.WaitForAssertion(() => shared.Find("[data-testid='cocktail-fork']"));
        // The shared catalog is read-only (JJ-002): its way to change is the fork button beside it.
        Assert.Empty(shared.FindAll("[data-testid='cocktail-edit']"));
    }
}
