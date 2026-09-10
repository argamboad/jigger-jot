using System.Net;
using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// INV-1's shelf screen, as reworked by INV-3. The API half has its own tests; these are about what
/// lives only in the component and that a server test cannot see: the running count, the optimistic
/// tick, the rollback when the save fails, and the four things INV-3 added — pills that are still
/// checkboxes, per-category counts, a jump bar carrying the same numbers, and a payoff footer that
/// re-asks for the makeable total without asking once per tick.
/// </summary>
public class ShelfPageTests : ComponentTestBase
{
    private const string Gin = "11111111-1111-1111-1111-111111111111";
    private const string Rum = "22222222-2222-2222-2222-222222222222";
    private const string Sloe = "33333333-3333-3333-3333-333333333333";

    private const string Shelf = $$"""
        [
          {"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":"London dry gin","isAvailable":false,"isOwn":false},
          {"id":"{{Sloe}}","name":"Sloe gin","category":"Gin","subcategory":"Sloe gin","isAvailable":false,"isOwn":false},
          {"id":"{{Rum}}","name":"White rum","category":"Rum","subcategory":"White rum","isAvailable":false,"isOwn":false}
        ]
        """;

    private const string Categories = """
        [{"id":"44444444-4444-4444-4444-444444444444","name":"Gin","subcategories":[
            {"id":"55555555-5555-5555-5555-555555555555","name":"London dry gin","subcategories":[]}]},
         {"id":"66666666-6666-6666-6666-666666666666","name":"Rum","subcategories":[]}]
        """;

    private static string Makeable(int total) =>
        $$"""{"items":[],"page":1,"pageSize":1,"total":{{total}}}""";

    private IRenderedComponent<Shelf> RenderShelf(int makeable = 0)
    {
        Http.On(HttpMethod.Get, "/api/inventory", Shelf);
        Http.On(HttpMethod.Get, "/api/cocktails", Makeable(makeable));
        return Render<Shelf>();
    }

    private static int AskedForTheCount(TestHttpHandler http) =>
        http.Requests.Count(r => r.Method == HttpMethod.Get
                              && r.RequestUri!.AbsolutePath == "/api/cocktails");

    [Fact]
    public void Shelf_ShowsTheCatalogGroupedByCategory()
    {
        var page = RenderShelf();

        page.WaitForAssertion(() =>
            Assert.Equal(3, page.FindAll("[data-testid^='shelf-item-']").Count));

        // Grouped, because 191 of these in a flat list is not a screen anyone fills in.
        var markup = page.Markup;
        Assert.Contains("Gin", markup);
        Assert.Contains("Rum", markup);
    }

    [Fact]
    public void APill_IsStillACheckbox()
    {
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        // INV-3 changed the control's appearance and nothing else. Bootstrap's .btn-check is a real
        // checkbox that is visually hidden and styled through its label, so the semantics, the
        // keyboard and the screen-reader announcement are the ones the browser already provides —
        // which is the whole reason for using it instead of drawing a pill and wiring clicks to it.
        var input = page.Find($"[data-testid='shelf-item-{Gin}']");
        Assert.Equal("checkbox", input.GetAttribute("type"));
        Assert.Contains("btn-check", input.GetAttribute("class"));

        // A hidden input is only operable through its label, so the pairing is load-bearing.
        var label = page.Find($"label[for='{input.GetAttribute("id")}']");
        Assert.Contains("London dry gin", label.TextContent);
    }

    [Fact]
    public void EachCategory_StatesItsCount_AndTheJumpBarSaysTheSame()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.NoContent);
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        Assert.Equal("Shelf_CategoryCount[0, 2]",
            page.Find("[data-testid='shelf-cat-count-gin']").TextContent.Trim());

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(true);

        // One number, rendered twice. The card and the jump link read the same value rather than
        // computing it separately, which is what stops them disagreeing after a tick.
        page.WaitForAssertion(() =>
        {
            Assert.Equal("Shelf_CategoryCount[1, 2]",
                page.Find("[data-testid='shelf-cat-count-gin']").TextContent.Trim());
            Assert.Contains("Shelf_CategoryCount[1, 2]",
                page.Find("[data-testid='shelf-jump-gin']").TextContent);
            Assert.Contains("Shelf_CategoryCount[0, 1]",
                page.Find("[data-testid='shelf-jump-rum']").TextContent);
        });
    }

    [Fact]
    public void TheJumpBar_LinksToTheCardsThemselves()
    {
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find("[data-testid='shelf-jump-gin']"));

        // Real links, so the keyboard and the browser's own history do the work. A link that points
        // at nothing is worse than no jump bar, so the anchor is asserted against the card's id.
        //
        // The path is part of the href on purpose: index.html carries <base href="/">, and a
        // fragment-only href resolves against the BASE rather than the current URL, so "#cat-gin"
        // means "/#cat-gin" — the home page. The browser found that one; this holds it fixed.
        Assert.Equal("/shelf#cat-gin", page.Find("[data-testid='shelf-jump-gin']").GetAttribute("href"));
        Assert.NotNull(page.Find("#cat-gin"));
        Assert.Equal("/shelf#cat-rum", page.Find("[data-testid='shelf-jump-rum']").GetAttribute("href"));
        Assert.NotNull(page.Find("#cat-rum"));
    }

    [Fact]
    public void ThePayoff_MovesWhenATickCompletesADrink()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.NoContent);
        var page = RenderShelf(makeable: 0);
        page.WaitForAssertion(() =>
            Assert.Contains("Shelf_PayoffNoDrinks", page.Find("[data-testid='shelf-payoff']").TextContent));

        // The bottle you tick is the one that completes a drink, so the footer has to say so — that
        // is the difference between filling in a form and watching something add up.
        Http.On(HttpMethod.Get, "/api/cocktails", Makeable(4));
        page.Find($"[data-testid='shelf-item-{Gin}']").Change(true);

        page.WaitForAssertion(
            () => Assert.Contains("Shelf_PayoffDrinks[4]",
                page.Find("[data-testid='shelf-payoff-drinks']").TextContent),
            TimeSpan.FromSeconds(10));

        // ...and once there is something to show, there is somewhere to go and see it.
        Assert.Equal("/cocktails?makeable=true",
            page.Find("[data-testid='shelf-payoff-show']").GetAttribute("href"));
    }

    [Fact]
    public async Task ABurstOfTicks_DoesNotAskForTheCountOncePerTick()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.NoContent);
        Http.On(HttpMethod.Put, $"/api/inventory/{Sloe}", status: HttpStatusCode.NoContent);
        Http.On(HttpMethod.Put, $"/api/inventory/{Rum}", status: HttpStatusCode.NoContent);
        var page = RenderShelf(makeable: 1);
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        var beforeTicking = AskedForTheCount(Http);

        // Someone filling in a shelf for the first time clicks straight down it. Asking the server
        // what that is worth after every one of those clicks is a request per checkbox, so the ask
        // waits for the clicking to stop and a burst collapses into one.
        // Found and clicked inside one dispatch each: the first tick re-renders the card it is in
        // (its count moves), so an element located before that render is a handle to a node the
        // renderer has already replaced.
        foreach (var id in new[] { Gin, Sloe, Rum })
        {
            await page.InvokeAsync(() => page.Find($"[data-testid='shelf-item-{id}']").Change(true));
        }

        page.WaitForAssertion(
            () => Assert.True(AskedForTheCount(Http) > beforeTicking, "the count was never refreshed"),
            TimeSpan.FromSeconds(10));

        // Asserted as an inequality rather than an exact number: what matters is that three ticks do
        // not cost three requests, not that the timer fired on a particular schedule.
        Assert.True(AskedForTheCount(Http) - beforeTicking < 3,
            $"three ticks cost {AskedForTheCount(Http) - beforeTicking} requests");
    }

    [Fact]
    public void AddingFromInsideACategory_PreselectsThatCategory()
    {
        Http.On(HttpMethod.Get, "/api/inventory/categories", Categories);
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find("[data-testid='shelf-add-in-gin']"));

        page.Find("[data-testid='shelf-add-in-gin']").Click();

        // The form belongs to the card it was opened from: you are standing in front of the gin
        // shelf holding a bottle of gin, and being made to say so again in a dropdown is the part
        // INV-2 got wrong by putting the button in the toolbar.
        page.WaitForAssertion(() =>
            Assert.Equal("44444444-4444-4444-4444-444444444444",
                page.Find("[data-testid='shelf-add-category']").GetAttribute("value")));
    }

    [Fact]
    public void Ticking_UpdatesTheCountImmediately()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.NoContent);
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(true);

        // The count is what tells a household how far it has got, so it has to move with the tick
        // rather than after a round trip.
        page.WaitForAssertion(() =>
            Assert.Contains("1", page.Find("[data-testid='shelf-count']").TextContent));
    }

    [Fact]
    public void Unticking_PutsTheCountBackDown()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.NoContent);
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(true);
        page.WaitForAssertion(() =>
            Assert.Contains("1", page.Find("[data-testid='shelf-count']").TextContent));

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(false);
        page.WaitForAssertion(() =>
            Assert.Contains("0", page.Find("[data-testid='shelf-count']").TextContent));
    }

    [Fact]
    public void AFailedSave_RollsTheTickBack_AndSaysSo()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.InternalServerError);
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(true);

        // Optimism is only honest if it is undone when the save fails. A tick that stays put after a
        // failed write is a lie the household finds out about later, when a drink they were promised
        // turns out not to be makeable.
        page.WaitForAssertion(() => page.Find("[data-testid='shelf-error']"));
        Assert.Contains("0", page.Find("[data-testid='shelf-count']").TextContent);
    }

    [Fact]
    public void AFailedLoad_SaysSo_RatherThanShowingAnEmptyShelf()
    {
        Http.On(HttpMethod.Get, "/api/inventory", "{}", HttpStatusCode.InternalServerError);

        var page = Render<Shelf>();

        // An empty shelf and a broken shelf look identical, and only one of them is the household's
        // fault to fix.
        page.WaitForAssertion(() => page.Find("[data-testid='shelf-error']"));
    }
}
