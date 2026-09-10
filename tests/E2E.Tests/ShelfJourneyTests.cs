using Microsoft.Playwright;

namespace JiggerJot.E2E.Tests;

/// <summary>
/// Shelf journey (INV): the checklist the whole product turns on. A household starts with nothing
/// ticked, ticks something, and finds it still ticked after a reload — which is the only way to see
/// that the optimistic checkbox actually reached the database rather than just the screen.
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
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("0", new() { Timeout = 30_000 });

        // Search narrows a 191-row list to something tickable, which is the point of having it.
        await Page.GetByTestId("shelf-search").FillAsync("London dry gin");
        var gin = Page.Locator("[data-testid^='shelf-item-']").First;
        await Expect(gin).ToBeVisibleAsync();
        await gin.CheckAsync();

        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("1");
        await Expect(Page.GetByTestId("shelf-error")).Not.ToBeVisibleAsync();

        // The tick is optimistic in the browser, so a reload is what proves it was persisted.
        await Page.ReloadAsync();
        await Expect(Page.GetByTestId("shelf-count")).ToContainTextAsync("1", new() { Timeout = 30_000 });

        // "Only what I have" is how a filled shelf stays readable once a household owns forty things.
        await Page.GetByTestId("shelf-only-available").CheckAsync();
        await Expect(Page.Locator("[data-testid^='shelf-item-']")).ToHaveCountAsync(1);
    }
}
