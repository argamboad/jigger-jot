using Microsoft.Playwright;

namespace JiggerJot.E2E.Tests;

/// <summary>
/// Makeable journeys (MAKE-1, FEATURES §9 and §11): the loop the product exists for. An empty shelf
/// makes nothing; tick the three things a Negroni needs and the Negroni appears; untick one and it
/// goes away again.
/// <para>
/// That last step is the one worth having. Makeability is derived at query time and never stored
/// (JJ-003), and the only way to see the difference between "derived" and "computed once and cached"
/// is to change the shelf and look again.
/// </para>
/// </summary>
[TestFixture]
public class MakeableJourneyTests : E2ETestBase
{
    private static readonly string[] Negroni = ["London dry gin", "Campari", "Sweet vermouth"];

    /// <summary>
    /// Opens the catalog and turns the makeable filter on. Waits for the unfiltered list FIRST,
    /// because a filter applied to a list that has not loaded is a race rather than a filter — the
    /// same mistake the shelf journey made before it was fixed.
    /// </summary>
    private async Task ShowMakeableAsync()
    {
        await Page.GetByTestId("nav-cocktails").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-list")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Page.GetByTestId("cocktail-makeable").CheckAsync();
    }

    /// <summary>Ticks one shared ingredient onto the shelf, waiting for the write rather than the paint.</summary>
    private async Task StockAsync(string ingredient)
    {
        var box = Page.GetByRole(AriaRole.Checkbox, new() { Name = ingredient, Exact = true });
        await Expect(box).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Page.RunAndWaitForResponseAsync(
            () => box.CheckAsync(),
            r => r.Url.Contains("/api/inventory/") && r.Request.Method == "PUT" && r.Status == 204);
    }

    [Test]
    public async Task WhatICanMake_FollowsMyShelf()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("make"));

        // Nothing on the shelf, so nothing to make — and the empty state points at the fix rather
        // than leaving someone to conclude the catalog is broken.
        await ShowMakeableAsync();
        await Expect(Page.GetByTestId("cocktail-empty")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await Page.GetByTestId("nav-shelf").ClickAsync();
        foreach (var ingredient in Negroni) await StockAsync(ingredient);

        await ShowMakeableAsync();
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Negroni", new() { Timeout = 30_000 });

        // Take one thing away and the drink goes with it, with nothing to recompute or invalidate.
        await Page.GetByTestId("nav-shelf").ClickAsync();
        var campari = Page.GetByRole(AriaRole.Checkbox, new() { Name = "Campari", Exact = true });
        await Expect(campari).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Page.RunAndWaitForResponseAsync(
            () => campari.UncheckAsync(),
            r => r.Url.Contains("/api/inventory/") && r.Request.Method == "PUT" && r.Status == 204);

        // The NEGRONI is gone, not the whole list. A household still holding gin and sweet vermouth
        // can make seven other things, including a sweet Martini — asserting an empty list here would
        // have been a claim about the data that was never true.
        await ShowMakeableAsync();
        await Page.GetByTestId("cocktail-search").FillAsync("Negroni");
        await Expect(Page.GetByTestId("cocktail-empty")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        // Turning the filter off is the other half of a filter: the catalog is still all there.
        await Page.GetByTestId("cocktail-search").FillAsync(string.Empty);
        await Page.GetByTestId("cocktail-makeable").UncheckAsync();
        await Expect(Page.GetByTestId("cocktail-list")).ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    [Test]
    public async Task ADrinkShownThanksToASubstitute_SaysWhatYouWouldPour()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("swap"));

        // A White Lady asks for Cointreau; this household has Curaçao, which the graph allows.
        await Page.GetByTestId("nav-shelf").ClickAsync();
        foreach (var ingredient in new[] { "London dry gin", "Curaçao", "Lemon juice" })
            await StockAsync(ingredient);

        await ShowMakeableAsync();
        await Page.GetByTestId("cocktail-search").FillAsync("White Lady");

        // FEATURES §9. Being told you can make a drink and finding the bottle missing at the shelf is
        // worse than not being told at all.
        await Expect(Page.GetByTestId("cocktail-substitution").First)
            .ToContainTextAsync("Curaçao", new() { Timeout = 30_000 });
    }
}
