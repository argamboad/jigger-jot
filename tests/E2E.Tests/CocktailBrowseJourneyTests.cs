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

    /// <summary>
    /// FILTER-1 (FEATURES §11): exploring the catalog rather than searching it. "Made with gin" is
    /// the filter someone actually wants, and it has to be wider than a name search — nobody types
    /// "London dry gin" when they mean gin (JJ-016).
    /// </summary>
    [Test]
    public async Task FilteringByIngredient_IsWiderThanSearching_AndCombinesWithTheRest()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("filter"));

        await Page.GetByTestId("nav-cocktails").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-list")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        var everything = await Page.GetByTestId("cocktail-count").InnerTextAsync();

        // Opening the panel fetches the dropdown options, so wait on that rather than on the markup.
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-filters-toggle").ClickAsync(),
            r => r.Url.Contains("/api/cocktails/filters") && r.Status == 200);

        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-filter-ingredient").FillAsync("gin"),
            r => r.Url.Contains("ingredient=gin") && r.Status == 200);

        // The catalog holds no drink CALLED gin, so every one of these came from a recipe line —
        // matched on the ingredient's name, its category or its subcategory.
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Negroni", new() { Timeout = 30_000 });
        var withGin = await Page.GetByTestId("cocktail-count").InnerTextAsync();
        Assert.That(withGin, Is.Not.EqualTo(everything));

        // Combinable, which is the whole claim of §11: stack a method on top and the list narrows
        // again rather than starting over.
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-filter-method").SelectOptionAsync(new SelectOptionValue { Index = 1 }),
            r => r.Url.Contains("method=") && r.Status == 200);
        await Expect(Page.GetByTestId("cocktail-filters-toggle")).ToContainTextAsync("(2)");

        // And clearing puts the whole catalog back, which is the half a one-way test never notices.
        await Page.GetByTestId("cocktail-filter-clear").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-count")).ToHaveTextAsync(everything, new() { Timeout = 30_000 });
    }

    /// <summary>
    /// FORK-1 (FEATURES §13): "create my own version". The copy opens, says what it was based on,
    /// carries every line, and is marked as the household's — and the original is untouched beside it.
    /// </summary>
    [Test]
    public async Task ForkingADrink_OpensMyOwnCopy_AndLeavesTheOriginalAlone()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("fork"));

        await Page.GotoAsync($"{BaseUrl}/cocktails");
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-search").FillAsync("negroni"),
            r => r.Url.Contains("search=negroni") && r.Status == 200);
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Negroni", new() { Timeout = 30_000 });
        await Page.GetByTestId("cocktail-list").Locator("a").First.ClickAsync();

        await Expect(Page.GetByTestId("cocktail-name")).ToContainTextAsync("Negroni", new() { Timeout = 30_000 });
        var originalUrl = Page.Url;
        var lines = await Page.GetByTestId("cocktail-lines").Locator("li").CountAsync();

        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-fork").ClickAsync(),
            r => r.Url.Contains("/fork") && r.Request.Method == "POST" && r.Status == 201);

        // Landing on the copy is the point: someone forks a drink because they want to change it.
        await Expect(Page).Not.ToHaveURLAsync(originalUrl);
        await Expect(Page.GetByTestId("cocktail-name")).ToContainTextAsync("Negroni", new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-lines").Locator("li")).ToHaveCountAsync(lines);

        // Provenance, not credit (JJ-013): "based on", never "written by".
        await Expect(Page.GetByTestId("cocktail-forked-from")).ToContainTextAsync("Negroni");
        await Expect(Page.GetByTestId("cocktail-source")).Not.ToBeVisibleAsync();

        // The original is still the book's, unchanged, right where it was.
        await Page.GotoAsync(originalUrl);
        await Expect(Page.GetByTestId("cocktail-source")).ToContainTextAsync("IBA", new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-forked-from")).Not.ToBeVisibleAsync();

        // And the copy sits in the catalog beside it, marked as the household's own.
        await Page.GotoAsync($"{BaseUrl}/cocktails");
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-search").FillAsync("negroni"),
            r => r.Url.Contains("search=negroni") && r.Status == 200);
        await Expect(Page.GetByTestId("cocktail-list").GetByText("Yours").First)
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
    }

    /// <summary>
    /// AUTHORING-1 (FEATURES §14): a household writes a cocktail from scratch. The last line of that
    /// flow is the one worth driving through a browser — the new drink joins the catalog and the
    /// makeable engine straight away, with nothing to rebuild and no tag to remember to set.
    /// </summary>
    [Test]
    public async Task WritingMyOwnCocktail_PutsItInTheCatalog_AndInWhatICanMake()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("author"));

        // Two bottles on the shelf first, so the drink written below is makeable the moment it exists.
        await Page.GetByTestId("nav-shelf").ClickAsync();
        await Expect(Page.Locator("[data-testid^='shelf-item-']").First)
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
        foreach (var ingredient in new[] { "London dry gin", "Campari" })
        {
            var box = Page.GetByRole(AriaRole.Checkbox, new() { Name = ingredient, Exact = true });
            await Page.RunAndWaitForResponseAsync(
                () => box.CheckAsync(),
                r => r.Url.Contains("/api/inventory/") && r.Request.Method == "PUT" && r.Status == 204);
        }

        await Page.GotoAsync($"{BaseUrl}/cocktails/new");
        await Expect(Page.GetByTestId("new-name")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        var name = $"House Special {Guid.NewGuid():N}"[..24];
        await Page.GetByTestId("new-name").FillAsync(name);
        await Page.GetByTestId("new-instructions").FillAsync("Stir, and do not overthink it.");

        // Two lines: the second one added, because a recipe with one ingredient would not exercise
        // the part of the form a person actually uses.
        await Page.GetByTestId("new-line-add").ClickAsync();
        await Expect(Page.GetByTestId("new-line")).ToHaveCountAsync(2);

        var ingredients = Page.GetByTestId("new-line-ingredient");
        await ingredients.Nth(0).SelectOptionAsync(new SelectOptionValue { Label = "London dry gin" });
        await Page.GetByTestId("new-line-amount").Nth(0).FillAsync("30");
        await Page.GetByTestId("new-line-unit").Nth(0).SelectOptionAsync(new SelectOptionValue { Label = "ml" });

        await ingredients.Nth(1).SelectOptionAsync(new SelectOptionValue { Label = "Campari" });
        await Page.GetByTestId("new-line-amount").Nth(1).FillAsync("30");
        await Page.GetByTestId("new-line-unit").Nth(1).SelectOptionAsync(new SelectOptionValue { Label = "ml" });

        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("new-save").ClickAsync(),
            r => r.Url.EndsWith("/api/cocktails") && r.Request.Method == "POST" && r.Status == 201);

        // It opens, with both lines and the amounts as written. Glass and method were left unstated,
        // which is allowed and shows as nothing rather than as a guess (JJ-034).
        await Expect(Page.GetByTestId("cocktail-name")).ToContainTextAsync(name, new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-lines").Locator("li")).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId("cocktail-lines")).ToContainTextAsync("30 ml");

        // And the claim §14 actually makes: it is makeable now, because makeability is derived from
        // the lines rather than stored (JJ-003).
        await Expect(Page.GetByTestId("cocktail-makeability")).ToContainTextAsync("You can make this");

        await Page.GotoAsync($"{BaseUrl}/cocktails");
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("cocktail-search").FillAsync(name),
            r => r.Url.Contains("search=") && r.Status == 200);
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync(name, new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Yours");
    }

    [Test]
    public async Task ChoosingImperial_ChangesWhatTheRecipeSays_AndChoosingAsWrittenPutsItBack()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("units"));

        // The Negroni is authored in millilitres, so it is the drink that shows the difference.
        await Page.GotoAsync($"{BaseUrl}/cocktails");
        await Page.GetByTestId("cocktail-search").FillAsync("negroni");
        await Expect(Page.GetByTestId("cocktail-list")).ToContainTextAsync("Negroni", new() { Timeout = 15_000 });
        await Page.GetByTestId("cocktail-list").GetByText("Negroni", new() { Exact = true }).First.ClickAsync();
        await Expect(Page.GetByTestId("cocktail-lines")).ToContainTextAsync("30 ml", new() { Timeout = 15_000 });
        var recipeUrl = Page.Url;

        // Settings, and the switcher saves server-side like the language and theme ones.
        await Page.GotoAsync($"{BaseUrl}/settings");
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("unit-switcher").SelectOptionAsync("Imperial"),
            r => r.Url.EndsWith("/api/auth/unit-system") && r.Request.Method == "PUT");

        // Same recipe, read in ounces. The stored 30 ml has not moved — only the reading of it.
        await Page.GotoAsync(recipeUrl);
        await Expect(Page.GetByTestId("cocktail-lines")).ToContainTextAsync("1 oz", new() { Timeout = 15_000 });

        // ...and "as written" is a choice a reader can come back to, not just where they started.
        await Page.GotoAsync($"{BaseUrl}/settings");
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("unit-switcher").SelectOptionAsync(""),
            r => r.Url.EndsWith("/api/auth/unit-system") && r.Request.Method == "PUT");

        await Page.GotoAsync(recipeUrl);
        await Expect(Page.GetByTestId("cocktail-lines")).ToContainTextAsync("30 ml", new() { Timeout = 15_000 });
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
