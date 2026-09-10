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
/// </summary>
[TestFixture]
public class ShelfJourneyTests : E2ETestBase
{
    [Test]
    public async Task Shelf_StartsEmpty_TakesATick_AndRemembersIt()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("shelf"));

        await Page.GetByTestId("nav-shelf").ClickAsync();

        // Wait for the LIST, not the counter: the counter renders immediately (as zero) while the
        // catalog is still loading, so asserting on it here would pass before there was anything to
        // tick.
        var gin = Page.GetByRole(AriaRole.Checkbox, new() { Name = "London dry gin", Exact = true });
        await Expect(gin).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(gin).Not.ToBeCheckedAsync();
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("0 ");

        await Page.RunAndWaitForResponseAsync(
            () => gin.CheckAsync(),
            r => r.Url.Contains("/api/inventory/") && r.Request.Method == "PUT" && r.Status == 204);

        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("1 ");
        await Expect(Page.GetByTestId("shelf-error")).Not.ToBeVisibleAsync();

        // The tick is optimistic in the browser, so a reload is what proves it was persisted.
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("1 ", new() { Timeout = 30_000 });

        // "Only what I have" is how a filled shelf stays readable once a household owns forty things.
        await Page.GetByTestId("shelf-only-available").CheckAsync();
        await Expect(Page.Locator("[data-testid^='shelf-item-']")).ToHaveCountAsync(1);

        // ...and unticking puts it back, which is the half a one-way test would never notice.
        await Page.GetByTestId("shelf-only-available").UncheckAsync();
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByRole(AriaRole.Checkbox, new() { Name = "London dry gin", Exact = true }).UncheckAsync(),
            r => r.Url.Contains("/api/inventory/") && r.Request.Method == "PUT" && r.Status == 204);

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

        // Opening the form fetches the category tree, so wait on that rather than on the markup.
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("shelf-add-toggle").ClickAsync(),
            r => r.Url.Contains("/api/inventory/categories") && r.Status == 200);

        var name = $"Homemade coffee liqueur {Guid.NewGuid():N}"[..40];
        await Page.GetByTestId("shelf-add-name").FillAsync(name);

        // Pick whatever the first real category is: this test is about the flow, not about the
        // catalog's contents, and naming a category here would be a claim about the seed data.
        var category = Page.GetByTestId("shelf-add-category");
        await category.SelectOptionAsync(new SelectOptionValue { Index = 1 });

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
        // implementation detail — and SelectOptionAsync waits for the option to exist regardless.
        await Page.GetByTestId("shelf-add-toggle").ClickAsync();
        await Page.GetByTestId("shelf-add-name").FillAsync(name.ToUpperInvariant());
        await Page.GetByTestId("shelf-add-category").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("shelf-add-submit").ClickAsync(),
            r => r.Url.EndsWith("/api/inventory/ingredients") && r.Request.Method == "POST" && r.Status == 409);

        await Expect(Page.GetByTestId("shelf-add-error")).ToBeVisibleAsync();
    }
}
