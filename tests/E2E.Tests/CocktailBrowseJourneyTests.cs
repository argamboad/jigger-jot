using Microsoft.Playwright;

namespace JiggerJot.E2E.Tests;

/// <summary>
/// Browse journey (CKTL-2): the first screen that reads the seeded catalog, so this is also the only
/// test in the suite that proves the seeder ran at startup against a real deployment rather than a
/// test fixture. A household that has added nothing still lands on nearly a thousand drinks.
/// <para>
/// The search leg is the one worth having. It is debounced in the browser and paged on the server,
/// which is exactly the combination where a unit test passes and the screen still shows the wrong
/// list — a slow response for an earlier keystroke arriving after a faster one for a later keystroke.
/// Driving it through the real box is the only way to see that.
/// </para>
/// </summary>
[TestFixture]
public class CocktailBrowseJourneyTests : E2ETestBase
{
    [Test]
    public async Task Catalog_IsBrowsable_Searchable_AndPaged()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("browse"));

        await Page.GetByTestId("nav-cocktails").ClickAsync();

        // The seeded catalog is there on first sign-in, with no setup by the household.
        await Expect(Page.GetByTestId("cocktail-list")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-count")).ToContainTextAsync("cocktails");
        var firstPageNames = await Page.GetByTestId("cocktail-list").Locator(".list-group-item").AllTextContentsAsync();
        Assert.That(firstPageNames, Has.Count.EqualTo(20));

        // Page two shows different drinks. Ordering is name-then-id precisely so that this holds
        // even though the catalog contains names that appear more than once.
        await Page.GetByTestId("cocktail-next").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-page")).ToContainTextAsync("2");
        var secondPageNames = await Page.GetByTestId("cocktail-list").Locator(".list-group-item").AllTextContentsAsync();
        Assert.That(secondPageNames.Intersect(firstPageNames), Is.Empty);

        // Search matches anywhere in the name, not just the front, and resets to the first page.
        await Page.GetByTestId("cocktail-search").FillAsync("martini");
        await Expect(Page.GetByTestId("cocktail-page")).ToContainTextAsync("1", new() { Timeout = 15_000 });
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Dry Martini");

        // A search with no hits is an empty state, not an error and not a stale list.
        await Page.GetByTestId("cocktail-search").FillAsync("zzzz no such drink");
        await Expect(Page.GetByTestId("cocktail-empty")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.GetByTestId("cocktail-error")).Not.ToBeVisibleAsync();

        // Clearing the box returns the whole catalog rather than leaving the empty state stuck.
        await Page.GetByTestId("cocktail-search").FillAsync(string.Empty);
        await Expect(Page.GetByTestId("cocktail-list")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task OpeningADrink_ShowsItsRecipe_ItsMethodAndItsCredit()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("detail"));

        await Page.GetByTestId("nav-cocktails").ClickAsync();
        await Page.GetByTestId("cocktail-search").FillAsync("negroni");
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Negroni", new() { Timeout = 15_000 });

        await Page.GetByTestId("cocktail-list").GetByText("Negroni", new() { Exact = true }).First.ClickAsync();

        await Expect(Page.GetByTestId("cocktail-name")).ToContainTextAsync("Negroni", new() { Timeout = 15_000 });

        // The three lines, with amounts rendered — the reader has no stored preference, so the recipe
        // reads exactly as the IBA wrote it (JJ-007).
        var lines = Page.GetByTestId("cocktail-lines").Locator("li");
        await Expect(lines).ToHaveCountAsync(3);
        await Expect(Page.GetByTestId("cocktail-lines")).ToContainTextAsync("30 ml");
        await Expect(Page.GetByTestId("cocktail-lines")).ToContainTextAsync("Campari");

        await Expect(Page.GetByTestId("cocktail-instructions")).ToContainTextAsync("Stir");

        // The credit is on the drink, not in a footer (JJ-032).
        await Expect(Page.GetByTestId("cocktail-source")).ToContainTextAsync("IBA");
    }

    [Test]
    public async Task ADrinkThatIsNotYours_IsNotFound()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("missing"));

        // A well-formed id that no household of ours owns: the page says so rather than erroring or
        // hanging on a spinner.
        await Page.GotoAsync($"{BaseUrl}/cocktails/{Guid.NewGuid()}");
        await Expect(Page.GetByTestId("cocktail-notfound")).ToBeVisibleAsync(new() { Timeout = 30_000 });
    }
}
