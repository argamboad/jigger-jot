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

    /// <summary>Ticks one shared ingredient onto the shelf. The how lives in the base, shared with
    /// every other journey that has to stock a shelf before it can test anything.</summary>
    private Task StockAsync(string ingredient) => SetShelfAsync(ingredient, wanted: true);

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
        await SetShelfAsync("Campari", wanted: false);

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
        // worse than not being told at all. MARGA-1 moved this out of a washed-out caveat and into
        // her voice — same data, same test id, and it still has to name the bottle.
        await Expect(Page.GetByTestId("cocktail-substitution").First)
            .ToContainTextAsync("Curaçao", new() { Timeout = 30_000 });

        // ...and she is the one saying it now.
        await Expect(Page.GetByTestId("cocktail-substitution").First.GetByTestId("marga"))
            .ToBeVisibleAsync();

        // On the recipe itself she says it once at the top, as the reason the drink qualified at
        // all, while the per-line "(you'd pour X)" marker stays to answer WHICH line (MARGA-1,
        // proposal screen 3).
        await Page.GetByTestId("cocktail-list").Locator("a").First.ClickAsync();
        await Expect(Page.GetByTestId("cocktail-marga")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-marga")).ToContainTextAsync("Curaçao");
        await Expect(Page.GetByTestId("cocktail-line-substitute").First).ToContainTextAsync("Curaçao");
    }

    /// <summary>
    /// ALMOST-1 (FEATURES §10): the shopping driver. Two thirds of a Negroni on the shelf should put
    /// the Negroni on the "one ingredient away" list, naming the vermouth — and buying the vermouth
    /// should move it to the other list. That last move is the journey; either half alone proves
    /// nothing about the two filters agreeing.
    /// </summary>
    [Test]
    public async Task OneIngredientAway_NamesTheBottle_AndBuyingItMovesTheDrink()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("almost"));

        await Page.GetByTestId("nav-shelf").ClickAsync();
        foreach (var ingredient in new[] { "London dry gin", "Campari" }) await StockAsync(ingredient);

        await Page.GetByTestId("nav-cocktails").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-list")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Page.GetByTestId("cocktail-almost").CheckAsync();
        await Page.GetByTestId("cocktail-search").FillAsync("Negroni");

        // The name is the feature. A row that only said "you cannot make this" would be the catalog.
        await Expect(Page.GetByTestId("cocktail-missing").First)
            .ToContainTextAsync("Sweet vermouth", new() { Timeout = 30_000 });

        await Page.GetByTestId("nav-shelf").ClickAsync();
        await StockAsync("Sweet vermouth");

        // Bought it: gone from "one away", arrived in "makeable". The two lists are adjacent and
        // never overlap, and nothing was recomputed or invalidated to make that true (JJ-003).
        await Page.GetByTestId("nav-cocktails").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-list")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Page.GetByTestId("cocktail-almost").CheckAsync();
        await Page.GetByTestId("cocktail-search").FillAsync("Negroni");
        await Expect(Page.GetByTestId("cocktail-empty")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        // Ticking "makeable now" unticks "one ingredient away": no drink is both, so the pair behaves
        // like a choice rather than two boxes that can contradict each other.
        await Page.GetByTestId("cocktail-makeable").CheckAsync();
        await Expect(Page.GetByTestId("cocktail-almost")).Not.ToBeCheckedAsync();
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Negroni", new() { Timeout = 30_000 });
    }

    /// <summary>
    /// MARGA-2 (proposal screen 1): the front page answers the product's question, and its buttons
    /// land on the list that produced the number they quote rather than on the whole catalog.
    /// </summary>
    [Test]
    public async Task TheHomeScreen_AnswersTheQuestion_AndItsButtonsGoWhereTheySay()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("home"));

        // An empty shelf gets her pointing at the shelf rather than a count of nothing.
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByTestId("home-marga")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("home-count")).Not.ToBeVisibleAsync();

        await Page.GetByTestId("nav-shelf").ClickAsync();
        foreach (var ingredient in Negroni) await StockAsync(ingredient);

        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByTestId("home-count")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        // The headline number and the list under it come from ONE response, so the count has to
        // match what the makeable filter says when you follow the button.
        var headline = (await Page.GetByTestId("home-count").InnerTextAsync()).Trim();
        await Page.GetByTestId("home-show-makeable").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-makeable")).ToBeCheckedAsync(new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-count")).ToContainTextAsync(headline);

        // ...and the other button lands on the other filter, with its summary card.
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByTestId("home-unlocks")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Page.GetByTestId("home-show-almost").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-almost")).ToBeCheckedAsync(new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-unlocks")).ToBeVisibleAsync();
    }

    /// <summary>
    /// ALMOST-2 (JJ-035): the same set the rows show, read the other way round. The summary names one
    /// bottle and the drinks it opens, and the number it gives has to match the names beside it.
    /// </summary>
    [Test]
    public async Task TheOneAwayList_LeadsWithTheBottleThatOpensTheMost()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("unlocks"));

        await Page.GetByTestId("nav-shelf").ClickAsync();
        foreach (var ingredient in new[] { "London dry gin", "Campari" }) await StockAsync(ingredient);

        await ShowMakeableAsync();
        await Page.GetByTestId("cocktail-makeable").UncheckAsync();
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-almost").CheckAsync(),
            r => r.Url.Contains("/api/cocktails/unlocks") && r.Status == 200);

        // The summary is the point of the slice: eighty-one rows each naming a bottle is correct and
        // unreadable, so the card says which single purchase opens the most.
        var card = Page.GetByTestId("cocktail-unlocks");
        await Expect(card).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(card).ToContainTextAsync("Sweet vermouth");
        await Expect(Page.GetByTestId("cocktail-unlocks-drinks")).ToContainTextAsync("Negroni");

        // It belongs to this filter only. Turning it off takes the card with it, because a shopping
        // suggestion over the whole catalog would be a claim nothing on screen supports.
        await Page.GetByTestId("cocktail-almost").UncheckAsync();
        await Expect(card).Not.ToBeVisibleAsync();
    }

    /// <summary>
    /// CKTL-4 (FEATURES §12): the recipe itself says where it stands. Opening a drink you are one
    /// bottle short of should mark the badge AND the line, so the reader knows what to do without
    /// counting the lines themselves.
    /// </summary>
    [Test]
    public async Task ARecipeSaysWhereItStands_AndWhichLineIsShort()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("detail"));

        await Page.GetByTestId("nav-shelf").ClickAsync();
        foreach (var ingredient in new[] { "London dry gin", "Campari" }) await StockAsync(ingredient);

        await Page.GetByTestId("nav-cocktails").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-list")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        // Wait for the SEARCH to come back before clicking a row. The box is debounced, so a click
        // that follows the keystroke straight away opens whatever was already on screen — which is
        // how this test first opened the Absinthe (Special) Cocktail and asked it about vermouth.
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-search").FillAsync("Negroni"),
            r => r.Url.Contains("search=Negroni") && r.Status == 200);
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Negroni", new() { Timeout = 30_000 });

        await Page.GetByTestId("cocktail-list").Locator("a").First.ClickAsync();

        await Expect(Page.GetByTestId("cocktail-name")).ToContainTextAsync("Negroni", new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-makeability")).ToContainTextAsync("One ingredient away");

        // The badge says how far; the line says which one. Both, or the reader is left comparing the
        // recipe against their own memory of the shelf.
        await Expect(Page.GetByTestId("cocktail-line-missing")).ToHaveCountAsync(1);
        await Expect(Page.GetByTestId("cocktail-lines")).ToContainTextAsync("Sweet vermouth");

        // Buy it, come back, and the same page says something different — because it was never stored.
        var recipeUrl = Page.Url;
        await Page.GetByTestId("nav-shelf").ClickAsync();
        await StockAsync("Sweet vermouth");
        await Page.GotoAsync(recipeUrl);

        await Expect(Page.GetByTestId("cocktail-makeability"))
            .ToContainTextAsync("You can make this", new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-line-missing")).ToHaveCountAsync(0);
    }
}
