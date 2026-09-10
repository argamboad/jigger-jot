using Microsoft.Playwright;

namespace JiggerJot.E2E.Tests;

/// <summary>
/// Shelf journey (INV-1): a household starts with nothing ticked, ticks something, and finds it still
/// ticked after a reload — which is the only way to see that the optimistic checkbox actually reached
/// the database rather than just the screen.
/// <para>
/// The tick waits on the PUT itself rather than on the counter changing. The counter moves optimistically
/// the instant the box is clicked, so waiting on it proves only that the browser re-rendered; waiting on
/// the response is what makes the rest of the test mean something. The first version of this test waited
/// on the counter and failed in CI for reasons no amount of reading it could explain.
/// </para>
/// <para>
/// INV-3 turned the checkboxes into pills, which is a real checkbox styled through its label and
/// therefore not clickable itself — the input carries <c>pointer-events: none</c>. So the journey
/// clicks the LABEL and asserts the state on the INPUT, which is also how a person uses it.
/// </para>
/// </summary>
[TestFixture]
public class ShelfJourneyTests : E2ETestBase
{
    /// <summary>The clickable half of a shelf pill.</summary>
    private ILocator Pill(string ingredient) =>
        Page.Locator("label.shelf-pill").Filter(new() { HasText = ingredient });

    /// <summary>The checkbox half, which is what actually holds the state.</summary>
    private ILocator Box(string ingredient) =>
        Page.GetByRole(AriaRole.Checkbox, new() { Name = ingredient, Exact = true });

    [Test]
    public async Task Shelf_StartsEmpty_TakesATick_AndRemembersIt()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("shelf"));

        await Page.GetByTestId("nav-shelf").ClickAsync();

        // Wait for the LIST, not the counter: the counter renders immediately (as zero) while the
        // catalog is still loading, so asserting on it here would pass before there was anything to
        // tick.
        await Expect(Pill("London dry gin")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Box("London dry gin")).Not.ToBeCheckedAsync();
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("0 ");

        await Page.RunAndWaitForResponseAsync(
            () => Pill("London dry gin").ClickAsync(),
            r => r.Url.Contains("/api/inventory/") && r.Request.Method == "PUT" && r.Status == 204);

        await Expect(Box("London dry gin")).ToBeCheckedAsync();
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("1 ");
        await Expect(Page.GetByTestId("shelf-error")).Not.ToBeVisibleAsync();

        // INV-3's payoff: the footer says what the shelf is now worth, and it asks the server for
        // that rather than deriving makeable a second time in the browser.
        await Expect(Page.GetByTestId("shelf-payoff")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("shelf-payoff-drinks")).Not.ToBeEmptyAsync(new() { Timeout = 15_000 });

        // The tick is optimistic in the browser, so a reload is what proves it was persisted.
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("1 ", new() { Timeout = 30_000 });

        // The count on the card is the same number the jump bar shows, because both read one value.
        var ginCard = Page.Locator("[data-testid^='shelf-cat-count-']").First;
        await Expect(ginCard).ToBeVisibleAsync();

        // "Only what I have" is how a filled shelf stays readable once a household owns forty things.
        await Page.GetByTestId("shelf-only-available").CheckAsync();
        await Expect(Page.Locator("[data-testid^='shelf-item-']")).ToHaveCountAsync(1);

        // ...and unticking puts it back, which is the half a one-way test would never notice.
        await Page.GetByTestId("shelf-only-available").UncheckAsync();
        await Page.RunAndWaitForResponseAsync(
            () => Pill("London dry gin").ClickAsync(),
            r => r.Url.Contains("/api/inventory/") && r.Request.Method == "PUT" && r.Status == 204);

        await Expect(Box("London dry gin")).Not.ToBeCheckedAsync();
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("0 ");
    }

    /// <summary>
    /// INV-2 (FEATURES §8): the bottle the catalog has never heard of. Add it inline, and it is on the
    /// shelf, ticked, and still there after a reload — which is the only way to tell a row that was
    /// written from a row that was merely drawn.
    /// </summary>
    [Test]
    public async Task ACustomIngredient_IsAddedInline_AndSticks()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("custom"));

        await Page.GetByTestId("nav-shelf").ClickAsync();

        // Wait for a real ROW, not the counter. The counter renders immediately, as zero, while the
        // catalog is still loading — the test above says so — so a click gated on it can land before
        // the page is interactive, and the request the next step waits for would never be made.
        await Expect(Page.Locator("[data-testid^='shelf-item-']").First)
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // INV-3 moved the button inside the category card it files the bottle into. Whichever card
        // comes first will do: this test is about the flow, not about the catalog's contents, and
        // naming a category here would be a claim about the seed data.
        var addInFirstCategory = Page.Locator("[data-testid^='shelf-add-in-']").First;

        // Opening the form fetches the category tree, so wait on that rather than on the markup.
        await Page.RunAndWaitForResponseAsync(
            () => addInFirstCategory.ClickAsync(),
            r => r.Url.Contains("/api/inventory/categories") && r.Status == 200);

        var name = $"Homemade coffee liqueur {Guid.NewGuid():N}"[..40];
        await Page.GetByTestId("shelf-add-name").FillAsync(name);

        // The category is already chosen, because the form was opened from inside one. That is the
        // whole point of moving it there, so the test asserts it rather than selecting again.
        await Expect(Page.GetByTestId("shelf-add-category")).Not.ToHaveValueAsync("");

        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("shelf-add-submit").ClickAsync(),
            r => r.Url.EndsWith("/api/inventory/ingredients") && r.Request.Method == "POST" && r.Status == 201);

        // Ticked on arrival: you add a bottle to your shelf because it is on your shelf.
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("1 ");
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("1 ", new() { Timeout = 30_000 });

        await Page.GetByTestId("shelf-only-available").CheckAsync();
        var mine = Page.GetByRole(AriaRole.Checkbox, new() { Name = name });
        await Expect(mine).ToBeCheckedAsync();

        // Adding it twice is a data-entry slip, not a second bottle — the form says so and points at
        // the one already there rather than leaving someone to hunt for a name they just typed.
        await Page.GetByTestId("shelf-only-available").UncheckAsync();

        // No explicit wait on the categories here. Whether they are re-fetched depends on whether the
        // reload above threw the cached tree away, so asserting either way would be asserting an
        // implementation detail — and the assertion below retries until the pre-selection lands.
        await addInFirstCategory.ClickAsync();
        await Expect(Page.GetByTestId("shelf-add-category")).Not.ToHaveValueAsync("");
        await Page.GetByTestId("shelf-add-name").FillAsync(name.ToUpperInvariant());
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("shelf-add-submit").ClickAsync(),
            r => r.Url.EndsWith("/api/inventory/ingredients") && r.Request.Method == "POST" && r.Status == 409);

        // The duplicate sends the search to the name, which may leave the category card the form was
        // opened from off the screen entirely — so the form comes back to the top of the page rather
        // than disappearing and taking this message with it.
        await Expect(Page.GetByTestId("shelf-add-error")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("shelf-search")).ToHaveValueAsync(name.ToUpperInvariant());
    }
}
