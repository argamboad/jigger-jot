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
/// MARGA-5: where she speaks, and — the harder half — where she does not.
/// <para>
/// She shipped on seven surfaces, but four were conditional and one was a flash, so on a normal
/// session with a filled shelf you met her once on the home page and then never again. This widened
/// that. The rule it follows is that she speaks where a number needs interpreting and stays quiet
/// where the screen already says it plainly, one Marga per screen — and the quiet half is what these
/// hold, because that is the half nobody notices breaking.
/// </para>
/// </summary>
public class MargaPresenceTests : ComponentTestBase
{
    private const string Gin = "11111111-1111-1111-1111-111111111111";

    /// <summary>A shelf with nothing ticked — where `unlocks` has nothing to rank.</summary>
    private const string Untouched = $$"""
        [{"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":false,"isOwn":false}]
        """;

    /// <summary>A shelf with something on it, which is what puts a bottle within one of more drinks.</summary>
    private const string Stocked = $$"""
        [{"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":true,"isOwn":false}]
        """;

    private const string OneBottleAway = """
        [{"ingredientId":"22222222-2222-2222-2222-222222222222","ingredient":"Sweet vermouth",
          "unlocks":4,"cocktails":["Negroni","Manhattan","Martinez","Rob Roy"]}]
        """;

    private const string OneStarter = """
        [{"ingredientId":"33333333-3333-3333-3333-333333333333","ingredient":"London dry gin","appears":40}]
        """;

    private static string Makeable(int total) =>
        $$"""{"items":[],"page":1,"pageSize":1,"total":{{total}}}""";

    // ── the shelf ────────────────────────────────────────────────────────────

    private IRenderedComponent<Shelf> RenderShelf(string catalog, string unlocks, string starters)
    {
        Http.On(HttpMethod.Get, "/api/inventory", catalog);
        Http.On(HttpMethod.Get, "/api/cocktails", Makeable(3));
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", unlocks);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", starters);
        return Render<Shelf>();
    }

    [Fact]
    public void OnTheShelf_SheNamesWhatIsMissing_NotWhatYouHave()
    {
        var page = RenderShelf(Stocked, OneBottleAway, OneStarter);

        page.WaitForAssertion(
            () => Assert.Equal("Marga_ShelfNext[Sweet vermouth, 4]",
                page.Find("[data-testid='shelf-marga']").TextContent.Trim()),
            TimeSpan.FromSeconds(10));

        // The footer two inches below already counts the bottles and the drinks. Her repeating that
        // number beside it is the thing that would turn her into wallpaper, so she takes the other
        // half of the story — what one more bottle would do.
        Assert.Contains("Shelf_Count", page.Find("[data-testid='shelf-count']").TextContent);
    }

    [Fact]
    public void OnAnUntouchedShelf_SheSuggestsWhereToStart()
    {
        // Nothing ticked, so nothing is one bottle away from anything and `unlocks` ranks nothing —
        // the gap MARGA-3 settled, answered here the same way.
        var page = RenderShelf(Untouched, "[]", OneStarter);

        page.WaitForAssertion(
            () => Assert.Equal("Marga_ShelfEmpty[London dry gin]",
                page.Find("[data-testid='shelf-marga']").TextContent.Trim()),
            TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void WithNothingToSuggest_SheSaysNothingAtAll()
    {
        // An empty shelf AND an empty catalog answer. An empty speech bubble is worse than no
        // bubble, so she is absent rather than blank.
        var page = RenderShelf(Untouched, "[]", "[]");

        page.WaitForAssertion(
            () => Assert.Contains("Shelf_Count", page.Find("[data-testid='shelf-count']").TextContent),
            TimeSpan.FromSeconds(10));

        Assert.Empty(page.FindAll("[data-testid='shelf-marga']"));
    }

    [Fact]
    public void HerLineFailing_DoesNotTakeTheFooterWithIt()
    {
        Http.On(HttpMethod.Get, "/api/inventory", Stocked);
        Http.On(HttpMethod.Get, "/api/cocktails", Makeable(7));
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", "{}", HttpStatusCode.InternalServerError);

        var page = Render<Shelf>();

        // Found by an existing test the moment her fetch joined the payoff's: they had shared a
        // catch, so failing to get her line blanked the drinks count as well. The footer states a
        // fact the screen owns; she is optional furniture, and the two must fail apart.
        page.WaitForAssertion(
            () => Assert.Contains("Shelf_PayoffDrinks[7]",
                page.Find("[data-testid='shelf-payoff-drinks']").TextContent),
            TimeSpan.FromSeconds(10));

        Assert.Empty(page.FindAll("[data-testid='shelf-marga']"));
    }

    // ── the catalog ──────────────────────────────────────────────────────────

    private IRenderedComponent<Cocktails> RenderCatalog(string url, int total)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        Http.On(HttpMethod.Get, "/api/cocktails", $$"""
            {"items":[{"id":"{{Gin}}","name":"Negroni","glass":null,"method":null,
              "servingType":"FullDrink","source":"IBA","isOwn":false,"ingredientCount":3,
              "substitutions":[],"missingIngredient":null}],
             "page":1,"pageSize":20,"total":{{total}}}
            """);
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", OneBottleAway);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", OneStarter);
        return Render<Cocktails>();
    }

    [Fact]
    public void UnderTheMakeableFilter_SheSaysTheCount()
    {
        var page = RenderCatalog("/cocktails?makeable=true", 12);

        // The one place she REPLACES a number rather than adding to one: under this filter the count
        // is the answer to the product's whole question, so she is the one who gives it.
        page.WaitForAssertion(() =>
            Assert.Contains("Marga_MakeableCount[12]",
                page.Find("[data-testid='cocktail-count']").TextContent));
    }

    [Fact]
    public void UnderAPlainBrowse_SheDoesNot()
    {
        var page = RenderCatalog("/cocktails", 969);

        // "969 cocktails" is a fact about the list. Nobody needs a character to read it out, and a
        // character who narrates every number stops being worth reading.
        page.WaitForAssertion(() =>
            Assert.Contains("Cocktails_Count[969]",
                page.Find("[data-testid='cocktail-count']").TextContent));

        Assert.DoesNotContain("Marga_MakeableCount", page.Markup);
    }

    [Fact]
    public void TheTestIdFollowsTheNumber_WhicheverElementCarriesIt()
    {
        // SHELL-1 moved information without moving its test id and took three suites down with it.
        // `cocktail-count` means "where the count is", and the journey that follows the home
        // screen's button reads it to check the two screens agree.
        foreach (var (url, total) in new[] { ("/cocktails?makeable=true", 12), ("/cocktails", 969) })
        {
            using var ctx = new MargaPresenceTests();
            var page = ctx.RenderCatalog(url, total);
            page.WaitForAssertion(() =>
                Assert.Contains(total.ToString(), page.Find("[data-testid='cocktail-count']").TextContent));
        }
    }
}
