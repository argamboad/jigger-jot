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
/// MARGA-6 (JJ-040): she speaks on every app screen that has something true to say, and the platform's
/// screens stay without her. Four places MARGA-5 had kept her quiet — a recipe more than one bottle
/// away, the plain and filtered catalog, a shelf search that finds no bottle, and the write form.
/// <para>
/// Every line is still one whole resource string with real data in it, and every one of these tests
/// pins which data: the missing lines the API marked, the count the API returned for the SAME filters,
/// the term that was typed, the bottles ticked on the shelf. And each has its quiet half, because a
/// line she has nothing behind is worse than no line.
/// </para>
/// </summary>
public class MargaEverywhereTests : ComponentTestBase
{
    // ── the recipe, more than one bottle away ────────────────────────────────

    private static readonly Guid RecipeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static string Line(string ingredient, string availability, bool required = true, string? substitute = null)
    {
        var req = required ? "true" : "false";
        var sub = substitute is null ? "null" : "\"" + substitute + "\"";
        return "{\"ingredient\":\"" + ingredient + "\",\"amount\":30,\"unit\":\"ml\",\"display\":\"30 ml\",\"isRequired\":" + req
             + ",\"role\":\"base\",\"notes\":null,\"availability\":\"" + availability + "\",\"substituteWith\":" + sub + "}";
    }

    private IRenderedComponent<CocktailDetail> RenderRecipe(string makeability, params string[] lines)
    {
        var json = "{\"id\":\"" + RecipeId + "\",\"name\":\"Negroni\",\"glass\":null,\"method\":\"Stir\","
                 + "\"servingType\":\"FullDrink\",\"instructions\":null,\"source\":null,\"isOwn\":false,"
                 + "\"makeability\":\"" + makeability + "\",\"forkedFrom\":null,\"lines\":[" + string.Join(",", lines) + "]}";
        Http.On(HttpMethod.Get, $"/api/cocktails/{RecipeId}", json);
        return Render<CocktailDetail>(ps => ps.Add(p => p.Id, RecipeId));
    }

    private static string HerLine(IRenderedComponent<CocktailDetail> page) =>
        page.Find("[data-testid='cocktail-marga'] .marga-line").TextContent.Trim();

    [Fact]
    public void TwoBottlesShort_SheNamesBoth_AndNotTheOptionalGarnish()
    {
        var page = RenderRecipe("NotMakeable",
            Line("London dry gin", "Available"),
            Line("Campari", "Missing"),
            Line("Sweet vermouth", "Missing"),
            Line("Orange twist", "Missing", required: false));

        // The garnish is missing too, but an optional line never blocks (JJ-009), so it is not one of
        // the bottles she says you are short of.
        page.WaitForAssertion(() =>
            Assert.Equal("Marga_RecipeShort[2, Marga_ListTwo[Campari, Sweet vermouth]]", HerLine(page)));
    }

    [Fact]
    public void ThreeBottlesShort_SheStillNamesThemAll()
    {
        var page = RenderRecipe("NotMakeable",
            Line("Campari", "Missing"), Line("Sweet vermouth", "Missing"), Line("Gin", "Missing"));

        page.WaitForAssertion(() =>
            Assert.Equal("Marga_RecipeShort[3, Marga_ListThree[Campari, Sweet vermouth, Gin]]", HerLine(page)));
    }

    [Fact]
    public void FourOrMoreShort_SheGivesTheCountAndTheFirstTwo()
    {
        // Past three, a list of every bottle is a shopping list nobody asked for; the count says how far
        // away the drink is and two names say where to start.
        var page = RenderRecipe("NotMakeable",
            Line("Campari", "Missing"), Line("Sweet vermouth", "Missing"),
            Line("Gin", "Missing"), Line("Orange bitters", "Missing"));

        page.WaitForAssertion(() =>
            Assert.Equal("Marga_RecipeFarShort[4, Campari, Sweet vermouth]", HerLine(page)));
    }

    [Fact]
    public void ASubstitutionStillOutranksHowFarAway()
    {
        var page = RenderRecipe("NotMakeable",
            Line("Cointreau", "Substitute", substitute: "Curaçao"),
            Line("Campari", "Missing"), Line("Sweet vermouth", "Missing"));

        page.WaitForAssertion(() => Assert.StartsWith("Marga_Swap[Cointreau, Curaçao]", HerLine(page)));
    }

    // ── the catalog, under Everything and under a filter ─────────────────────

    private static string ListOf(int total) => $$"""
        {"items":[{"id":"22222222-2222-2222-2222-222222222222","name":"Negroni","glass":null,"method":null,
          "servingType":"FullDrink","source":"IBA","isOwn":false,"ingredientCount":3,
          "substitutions":[],"missingIngredient":null}],
         "page":1,"pageSize":20,"total":{{total}}}
        """;

    private static string CountOf(int total) => $$"""{"items":[],"page":1,"pageSize":1,"total":{{total}}}""";

    private static bool AsksWhatIsPourable(HttpRequestMessage r) =>
        r.RequestUri!.Query.Contains("makeable=true", StringComparison.Ordinal);

    private IRenderedComponent<Cocktails> RenderCatalog(string url, int total, string pourable)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        Http.On(HttpMethod.Get, "/api/cocktails", r => AsksWhatIsPourable(r) ? pourable : ListOf(total));
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", "[]");
        Http.On(HttpMethod.Get, "/api/cocktails/starters", "[]");
        return Render<Cocktails>();
    }

    [Fact]
    public void UnderEverything_SheReadsTheListAgainstTheShelf()
    {
        var page = RenderCatalog("/cocktails", 23, CountOf(6));

        // The id follows the number (MARGA-5): her sentence carries the count now, so it carries the id.
        page.WaitForAssertion(() =>
        {
            var count = page.Find("[data-testid='cocktail-count']");
            Assert.Contains("Marga_BrowseCount[23, 6]", count.TextContent);
            Assert.NotNull(count.QuerySelector(".marga-card"));
        });
    }

    [Fact]
    public void WhenNoneArePourable_SheSaysThat_InsteadOfZero()
    {
        var page = RenderCatalog("/cocktails", 23, CountOf(0));

        page.WaitForAssertion(() =>
            Assert.Contains("Marga_BrowseCountNone[23]", page.Find("[data-testid='cocktail-count']").TextContent));
    }

    [Fact]
    public void TheCountSheQuotes_IsAskedWithTheSameFilters()
    {
        var page = RenderCatalog("/cocktails", 23, CountOf(6));
        page.WaitForAssertion(() => Assert.Contains("Marga_BrowseCount", page.Markup));

        // "You can pour 6 of them" is only true of THESE drinks, so the count has to be asked of the
        // same search the list was — one row of it, under the makeable filter.
        page.Find("[data-testid='cocktail-search']").Input("gin");
        page.WaitForAssertion(() => Assert.Contains(Http.Requests, r =>
            AsksWhatIsPourable(r)
            && r.RequestUri!.Query.Contains("search=gin", StringComparison.Ordinal)
            && r.RequestUri.Query.Contains("pageSize=1", StringComparison.Ordinal)), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IfTheCountFails_ThePlainLineStands()
    {
        // She is optional furniture: a count the screen could not get is not a reason to lose the one
        // it did.
        var page = RenderCatalog("/cocktails", 23, "not json");

        page.WaitForAssertion(() =>
        {
            var count = page.Find("[data-testid='cocktail-count']");
            Assert.Contains("Cocktails_Count[23]", count.TextContent);
            Assert.Null(count.QuerySelector(".marga-card"));
        });
    }

    [Fact]
    public void UnderOneAway_SheLeavesTheCountToThePanel()
    {
        // The one-away list already has her panel naming the bottle; a second Marga reading the count
        // above it would be two of her on one screen. And no count request is made for it.
        var page = RenderCatalog("/cocktails?almost=true", 23, CountOf(6));

        page.WaitForAssertion(() =>
            Assert.Contains("Cocktails_Count[23]", page.Find("[data-testid='cocktail-count']").TextContent));
        Assert.DoesNotContain("Marga_BrowseCount", page.Markup);
        Assert.DoesNotContain(Http.Requests, AsksWhatIsPourable);
    }

    // ── the shelf, when a search finds no bottle ─────────────────────────────

    private const string ShelfJson = """
        [
          {"id":"33333333-3333-3333-3333-333333333333","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":false,"isOwn":false},
          {"id":"44444444-4444-4444-4444-444444444444","name":"Sloe gin","category":"Gin","subcategory":null,"isAvailable":false,"isOwn":false},
          {"id":"55555555-5555-5555-5555-555555555555","name":"White rum","category":"Rum","subcategory":null,"isAvailable":true,"isOwn":false}
        ]
        """;

    private IRenderedComponent<Shelf> RenderShelf()
    {
        Http.On(HttpMethod.Get, "/api/inventory", ShelfJson);
        Http.On(HttpMethod.Get, "/api/cocktails", CountOf(0));
        var page = Render<Shelf>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-testid^='shelf-item-']")));
        return page;
    }

    [Fact]
    public void ASearchForABottleNobodyStocks_SheSaysSo_AsAFootnote()
    {
        var page = RenderShelf();

        page.Find("[data-testid='shelf-search']").Input("yuzu");

        // Inline, not the page's voice: the shelf's own Marga at the top stays the one page-level line.
        page.WaitForAssertion(() =>
        {
            var note = page.Find("[data-testid='shelf-nomatch']");
            Assert.Contains("Marga_ShelfNoMatch[yuzu]", note.TextContent);
            Assert.NotNull(note.QuerySelector(".marga-inline"));
            Assert.NotNull(page.Find("[data-testid='shelf-add-toggle']"));
        });
    }

    [Fact]
    public void ABottleOnlyHiddenByTheFilter_IsNotCalledUnknown()
    {
        var page = RenderShelf();

        // Sloe gin is on the list — just not ticked, so "only what I have" hides it. "Nothing called
        // sloe on my list" would be false; the plain line says what is actually true.
        page.Find("[data-testid='shelf-only-available']").Change(true);
        page.Find("[data-testid='shelf-search']").Input("sloe");

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Shelf_NoMatches", page.Find("[data-testid='shelf-empty']").TextContent);
            Assert.Empty(page.FindAll("[data-testid='shelf-nomatch']"));
        });
    }

    // ── the write form ───────────────────────────────────────────────────────

    private const string Lookups = """
        {"glasses":[],"methods":[],"units":[],"servingTypes":["FullDrink","Shot"],"roles":["Base"]}
        """;

    private IRenderedComponent<WriteCocktail> RenderForm(string inventory, HttpStatusCode status = HttpStatusCode.OK)
    {
        Http.On(HttpMethod.Get, "/api/cocktails/lookups", Lookups);
        Http.On(HttpMethod.Get, "/api/inventory", inventory, status);
        var page = Render<WriteCocktail>();
        page.WaitForAssertion(() => page.Find("[data-testid='new-name']"));
        return page;
    }

    [Fact]
    public void OnTheForm_SheCountsTheBottlesOnTheShelf()
    {
        var page = RenderForm(ShelfJson.Replace("\"isAvailable\":false", "\"isAvailable\":true").Replace(
            "\"name\":\"Sloe gin\",\"category\":\"Gin\",\"subcategory\":null,\"isAvailable\":true",
            "\"name\":\"Sloe gin\",\"category\":\"Gin\",\"subcategory\":null,\"isAvailable\":false"));

        page.WaitForAssertion(() =>
        {
            var marga = page.Find("[data-testid='new-marga']");
            Assert.Equal("Marga_WriteShelf[2]", marga.QuerySelector(".marga-line")!.TextContent.Trim());
            Assert.NotNull(marga.QuerySelector(".marga-card"));
        });
    }

    [Fact]
    public void OnAnEmptyShelf_SheSaysWriteItAnyway()
    {
        var page = RenderForm(ShelfJson.Replace("\"isAvailable\":true", "\"isAvailable\":false"));

        page.WaitForAssertion(() =>
            Assert.Equal("Marga_WriteEmpty", page.Find("[data-testid='new-marga'] .marga-line").TextContent.Trim()));
    }

    [Fact]
    public void IfTheShelfCannotBeRead_SheIsAbsent()
    {
        var page = RenderForm("{}", HttpStatusCode.InternalServerError);

        Assert.Empty(page.FindAll("[data-testid='new-marga']"));
    }
}
