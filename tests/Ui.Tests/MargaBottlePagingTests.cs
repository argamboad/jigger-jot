using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// MARGA-7: wherever Marga names a bottle to buy, ‹ › pages through the next ones — up to ten, as many
/// as the ranking has, stopping at both ends. Five places, two rankings: the bottles that open up drinks
/// one away (Home, the shelf, the catalog's one-away panel) and, on an empty shelf, where to start (the
/// shelf and the catalog's empty state). Her first line stays as it was; later ones say "or".
/// </summary>
public class MargaBottlePagingTests : ComponentTestBase
{
    private const string Gin = "11111111-1111-1111-1111-111111111111";
    private const string Absinthe = "22222222-2222-2222-2222-222222222222";
    private const string Vodka = "33333333-3333-3333-3333-333333333333";
    private const string DryVermouth = "44444444-4444-4444-4444-444444444444";

    private const string ThreeBottles = $$"""
        [{"ingredientId":"{{Absinthe}}","ingredient":"Absinthe","unlocks":57,
          "cocktails":["Absinthe Cocktail","Absinthe Drip","Blackthorn","Bombay (No. 2)","Corpse Reviver"]},
         {"ingredientId":"{{Vodka}}","ingredient":"Vodka","unlocks":31,
          "cocktails":["Screwdriver","Moscow Mule","Cosmopolitan"]},
         {"ingredientId":"{{DryVermouth}}","ingredient":"Dry vermouth","unlocks":2,
          "cocktails":["Martini","Manhattan"]}]
        """;

    private const string OneBottle = $$"""
        [{"ingredientId":"{{Absinthe}}","ingredient":"Absinthe","unlocks":57,"cocktails":["Absinthe Cocktail"]}]
        """;

    private const string ThreeStarters = $$"""
        [{"ingredientId":"{{Gin}}","ingredient":"London dry gin","appears":40},
         {"ingredientId":"{{Vodka}}","ingredient":"Lime juice","appears":22},
         {"ingredientId":"{{DryVermouth}}","ingredient":"Campari","appears":12}]
        """;

    private static string Makeable(int total) => $$"""{"items":[],"page":1,"pageSize":3,"total":{{total}}}""";

    private static string Position<T>(IRenderedComponent<T> page) where T : IComponent =>
        page.Find("[data-testid='bottle-position']").TextContent.Trim();

    private bool Asked(string path) => Http.Requests.Any(r =>
        r.RequestUri!.AbsolutePath == path && r.RequestUri.Query.Contains("limit=10"));

    // ── Home ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Home_PagesThroughTheBottles_HerLineAndThePanelMovingTogether()
    {
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/cocktails", Makeable(5));
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", ThreeBottles);

        var cut = Render<Home>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Marga_HomeBottle[Absinthe, 57]", cut.Find("[data-testid='home-marga'] .marga-line").TextContent.Trim());
            Assert.Equal("Marga_BottlePosition[1, 3]", Position(cut));
        });
        Assert.Contains("Blackthorn", cut.Find("[data-testid='home-unlocks']").TextContent);
        Assert.True(Asked("/api/cocktails/unlocks"), "Home asks for up to ten bottles");

        await cut.Find("[data-testid='bottle-next']").ClickAsync(new());

        // The second bottle is an alternative to the first, so she says "or" rather than repeating
        // herself — and the box under her line lists that bottle's drinks.
        Assert.Equal("Marga_HomeBottleOr[Vodka, 31]", cut.Find("[data-testid='home-marga'] .marga-line").TextContent.Trim());
        var panel = cut.Find("[data-testid='home-unlocks']").TextContent;
        Assert.Contains("Screwdriver", panel);
        Assert.DoesNotContain("Blackthorn", panel);
        Assert.Equal("Marga_BottlePosition[2, 3]", Position(cut));
    }

    [Fact]
    public async Task Home_WithNothingPourableYet_ThePanelStillPages()
    {
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/cocktails", Makeable(0));
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", ThreeBottles);

        var cut = Render<Home>();
        cut.WaitForAssertion(() => cut.Find("[data-testid='bottle-next']"));

        await cut.Find("[data-testid='bottle-next']").ClickAsync(new());
        await cut.Find("[data-testid='bottle-next']").ClickAsync(new());

        Assert.Contains("Cocktails_UnlocksHeadline[Dry vermouth, 2]", cut.Find("[data-testid='home-unlocks']").TextContent);
        Assert.True(cut.Find("[data-testid='bottle-next']").HasAttribute("disabled"), "it stops at the last bottle");
    }

    [Fact]
    public async Task Home_WithOnlyOneBottle_HasNoArrows()
    {
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/cocktails", Makeable(5));
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", OneBottle);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='home-unlocks']"));
        Assert.Empty(cut.FindAll("[data-testid='bottle-pager']"));
    }

    // ── the shelf ────────────────────────────────────────────────────────────

    private const string StockedWithTwoMissing = $$"""
        [{"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":true,"isOwn":false},
         {"id":"{{Absinthe}}","name":"Absinthe","category":"Absinthe and pastis","subcategory":null,"isAvailable":false,"isOwn":false},
         {"id":"{{Vodka}}","name":"Vodka","category":"Vodka","subcategory":null,"isAvailable":false,"isOwn":false}]
        """;

    private const string Untouched = $$"""
        [{"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":false,"isOwn":false}]
        """;

    private IRenderedComponent<Shelf> RenderShelf(string catalog, string unlocks, string starters)
    {
        Http.On(HttpMethod.Get, "/api/inventory", catalog);
        Http.On(HttpMethod.Get, "/api/cocktails", Makeable(3));
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", unlocks);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", starters);
        return Render<Shelf>();
    }

    private static string PillClasses(IRenderedComponent<Shelf> page, string ingredientId)
    {
        var input = page.Find($"[data-testid='shelf-item-{ingredientId}']");
        return page.Find($"label[for='{input.GetAttribute("id")}']").ClassName ?? "";
    }

    [Fact]
    public async Task Shelf_PagesThroughTheBottles_AndTheOutlinedPillFollows()
    {
        var page = RenderShelf(StockedWithTwoMissing, ThreeBottles, "[]");

        page.WaitForAssertion(
            () => Assert.Equal("Marga_ShelfNext[Absinthe, 57]", page.Find("[data-testid='shelf-marga'] .marga-line").TextContent.Trim()),
            TimeSpan.FromSeconds(10));
        Assert.Contains("shelf-pill-named", PillClasses(page, Absinthe));
        Assert.True(Asked("/api/cocktails/unlocks"), "the shelf asks for up to ten bottles");

        await page.Find("[data-testid='bottle-next']").ClickAsync(new());

        Assert.Equal("Marga_ShelfNextOr[Vodka, 31]", page.Find("[data-testid='shelf-marga'] .marga-line").TextContent.Trim());
        // The bottle she names is the one outlined, so it can still be found on a long shelf.
        Assert.Contains("shelf-pill-named", PillClasses(page, Vodka));
        Assert.DoesNotContain("shelf-pill-named", PillClasses(page, Absinthe));
    }

    [Fact]
    public async Task Shelf_WhenTheShelfChanges_SheStartsAgainFromTheBestBottle()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Absinthe}", "{}");
        var page = RenderShelf(StockedWithTwoMissing, ThreeBottles, "[]");
        page.WaitForAssertion(() => page.Find("[data-testid='bottle-next']"), TimeSpan.FromSeconds(10));

        await page.Find("[data-testid='bottle-next']").ClickAsync(new());
        Assert.Equal("Marga_BottlePosition[2, 3]", Position(page));

        // A tick re-ranks the bottles; staying on "2 of 3" would point at whatever is second now.
        page.Find($"[data-testid='shelf-item-{Absinthe}']").Change(true);

        page.WaitForAssertion(() => Assert.Equal("Marga_BottlePosition[1, 3]", Position(page)), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Shelf_WithNothingTicked_PagesThroughWhereToStart()
    {
        var page = RenderShelf(Untouched, "[]", ThreeStarters);

        page.WaitForAssertion(
            () => Assert.Equal("Marga_ShelfEmpty[London dry gin]", page.Find("[data-testid='shelf-marga'] .marga-line").TextContent.Trim()),
            TimeSpan.FromSeconds(10));
        Assert.True(Asked("/api/cocktails/starters"), "the shelf asks for up to ten starting bottles");

        await page.Find("[data-testid='bottle-next']").ClickAsync(new());

        // Still "recipes ask for it", never "opens up": on an empty shelf the counts overlap.
        Assert.Equal("Marga_ShelfEmptyOr[Lime juice, 22]", page.Find("[data-testid='shelf-marga'] .marga-line").TextContent.Trim());
    }

    // ── the catalog ──────────────────────────────────────────────────────────

    private IRenderedComponent<Cocktails> RenderCatalog(int total, string unlocks, string starters)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/cocktails?almost=true");
        Http.On(HttpMethod.Get, "/api/cocktails", total == 0
            ? """{"items":[],"page":1,"pageSize":20,"total":0}"""
            : $$"""
              {"items":[{"id":"{{Gin}}","name":"Blackthorn","glass":null,"method":null,
                "servingType":"FullDrink","source":"IBA","isOwn":false,"ingredientCount":3,
                "substitutions":[],"missingIngredient":"Absinthe"}],
               "page":1,"pageSize":20,"total":{{total}}}
              """);
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", unlocks);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", starters);
        return Render<Cocktails>();
    }

    [Fact]
    public async Task Catalog_TheOneAwayPanel_PagesThroughTheBottles_AndItsDrinksFollow()
    {
        var page = RenderCatalog(90, ThreeBottles, "[]");

        page.WaitForAssertion(() =>
            Assert.Equal("Marga_UnlocksHeadline[Absinthe, 57]", page.Find("[data-testid='cocktail-unlocks'] .marga-line").TextContent.Trim()));
        Assert.Contains("Blackthorn", page.Find("[data-testid='cocktail-unlocks-drinks']").TextContent);
        Assert.True(Asked("/api/cocktails/unlocks"), "the panel asks for up to ten bottles");

        // Her line is announced when it changes, since the arrows change it without moving focus.
        Assert.NotNull(page.Find("[data-testid='cocktail-unlocks'] [aria-live='polite'] .marga-line"));

        await page.Find("[data-testid='bottle-next']").ClickAsync(new());

        Assert.Equal("Marga_UnlocksHeadlineOr[Vodka, 31]", page.Find("[data-testid='cocktail-unlocks'] .marga-line").TextContent.Trim());
        var drinks = page.Find("[data-testid='cocktail-unlocks-drinks']").TextContent;
        Assert.Contains("Screwdriver", drinks);
        Assert.DoesNotContain("Blackthorn", drinks);

        // The list below is the one-away list itself, and paging her suggestion does not filter it.
        Assert.Contains("Blackthorn", page.Find("[data-testid='cocktail-list']").TextContent);

        await page.Find("[data-testid='bottle-prev']").ClickAsync(new());
        Assert.Equal("Marga_UnlocksHeadline[Absinthe, 57]", page.Find("[data-testid='cocktail-unlocks'] .marga-line").TextContent.Trim());
        Assert.True(page.Find("[data-testid='bottle-prev']").HasAttribute("disabled"), "it stops at the best bottle");
    }

    [Fact]
    public async Task Catalog_NothingOneAway_PagesThroughWhereToStart()
    {
        var page = RenderCatalog(0, "[]", ThreeStarters);

        page.WaitForAssertion(() =>
            Assert.Equal("Marga_StarterBottle[London dry gin, 40]", page.Find("[data-testid='cocktail-empty-starter']").TextContent.Trim()));
        Assert.True(Asked("/api/cocktails/starters"), "the empty state asks for up to ten starting bottles");

        await page.Find("[data-testid='bottle-next']").ClickAsync(new());

        Assert.Equal("Marga_StarterBottleOr[Lime juice, 22]", page.Find("[data-testid='cocktail-empty-starter']").TextContent.Trim());
        Assert.Equal("Marga_BottlePosition[2, 3]", Position(page));
    }
}
