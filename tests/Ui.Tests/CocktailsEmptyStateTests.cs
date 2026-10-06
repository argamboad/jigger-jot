using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// MARGA-3, proposal screens 6 and 7 — the two empty states.
/// <para>
/// Covered here rather than in a browser journey because the state itself is a cold start: an
/// almost-makeable list that comes back EMPTY means the catalog holds no recipe within one bottle of
/// this household, which the developer database cannot produce. It was seeded before the shipped
/// catalog was cut to its starter set and still holds single-ingredient recipes, so an empty shelf
/// there is one bottle away from fourteen drinks.
/// </para>
/// </summary>
public class CocktailsEmptyStateTests : ComponentTestBase
{
    private const string NoCocktails = """{"items":[],"page":1,"pageSize":20,"total":0}""";

    private const string OneStarter = """
        [{"ingredientId":"11111111-1111-1111-1111-111111111111","ingredient":"London dry gin","appears":9}]
        """;

    private IRenderedComponent<Cocktails> RenderAt(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        Http.On(HttpMethod.Get, "/api/cocktails", NoCocktails);
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", "[]");
        Http.On(HttpMethod.Get, "/api/cocktails/starters", OneStarter);
        return Render<Cocktails>();
    }

    [Fact]
    public void NothingMakeable_PointsAtTheShelf_RatherThanStatingAVerdict()
    {
        var page = RenderAt("/cocktails?makeable=true");

        page.WaitForAssertion(() =>
        {
            // The complaint this screen answers: it used to read as a broken catalog rather than as
            // an empty shelf, and it offered a text link where the whole screen is one action.
            Assert.Contains("Make_NothingYet", page.Find("[data-testid='cocktail-empty']").TextContent);
            Assert.Equal("/shelf", page.Find("[data-testid='cocktail-empty-shelf']").GetAttribute("href"));
        });

        // Nothing to buy is named here. With nothing ticked the action is to say what you have, not
        // to go shopping — that only makes sense once the shelf has been filled in and STILL nothing
        // is within reach.
        Assert.Empty(page.FindAll("[data-testid='cocktail-empty-starter']"));
    }

    [Fact]
    public void NothingOneAway_NamesABottleToStartFrom()
    {
        var page = RenderAt("/cocktails?almost=true");

        page.WaitForAssertion(() =>
        {
            // Her line names the bottle and how many recipes ASK for it. It never promises drinks:
            // one bottle on an empty shelf makes very nearly nothing, and ALMOST-2 — which could
            // promise drinks, because it measured them — has nothing to say in this state.
            Assert.Equal("Marga_StarterBottle[London dry gin, 9]",
                page.Find("[data-testid='cocktail-empty-starter']").TextContent.Trim());
            Assert.Equal("/shelf", page.Find("[data-testid='cocktail-empty-shelf']").GetAttribute("href"));
        });
    }

    [Fact]
    public async Task ASearchThatFoundNothing_IsNotAHouseholdStartingFromNothing()
    {
        var page = RenderAt("/cocktails?almost=true");
        page.WaitForAssertion(() => page.Find("[data-testid='cocktail-empty-starter']"));

        // Typed rather than put in the URL: the page reads the two makeability toggles off the query
        // string and nothing else, so a `search=` parameter would not reach the field it is about.
        await page.InvokeAsync(() => page.Find("[data-testid='cocktail-search']").Input("zzzz"));

        page.WaitForAssertion(
            () => Assert.Empty(page.FindAll("[data-testid='cocktail-empty-starter']")),
            TimeSpan.FromSeconds(10));

        // Found in the browser. An empty list under a search means the SEARCH found nothing, and
        // answering that with "starting from nothing? get gin" tells someone who may own forty
        // bottles to go shopping. The one-away filter has to be the only thing narrowing the list
        // for its emptiness to say anything at all about the shelf.
        Assert.Contains("Almost_NothingYet", page.Find("[data-testid='cocktail-empty']").TextContent);
    }

    [Fact]
    public void AnEmptyCatalogSearch_IsNotAnEmptyShelf()
    {
        var page = RenderAt("/cocktails?search=zzzz");

        page.WaitForAssertion(() =>
            Assert.Contains("Cocktails_NoMatches", page.Find("[data-testid='cocktail-empty']").TextContent));

        // No illustration, no shelf button, no suggestion: an unfiltered catalog search returning
        // nothing says something about the search, not about the household.
        Assert.Empty(page.FindAll("[data-testid='cocktail-empty-shelf']"));
        Assert.Empty(page.FindAll("[data-testid='cocktail-empty-starter']"));
    }
}
