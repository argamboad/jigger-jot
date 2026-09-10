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
}
