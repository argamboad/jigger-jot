using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace JiggerJot.E2E.Tests;

/// <summary>
/// ONBOARD-1 (FEATURES §7): the first minute. A brand-new household has an empty shelf, so "what can
/// I make" would answer nothing — this walks the whole way from that cold start to a populated list.
/// <para>
/// It is the one journey that can prove the wizard's central rule end to end: <b>nothing is written
/// until Finish</b>. The steps are driven, a suggestion is unticked, and the shelf is only checked
/// afterwards — so a wizard that wrote as it went would show up here as a shelf that disagrees with
/// what was confirmed.
/// </para>
/// </summary>
[TestFixture]
public class OnboardJourneyTests : E2ETestBase
{
    [Test]
    public async Task AColdStart_GoesFromAnEmptyShelfToSomethingToPour()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("onboard"));

        // The front page of a household that has ticked nothing offers the guided route. 191 pills
        // with no idea which matter is the dead first impression JJ-021 exists to avoid.
        var setUp = Page.GetByTestId("home-setup-shelf");
        await Expect(setUp).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await setUp.ClickAsync();

        // Step one is the suggestions, and they arrive ticked — answering "which of these is wrong"
        // is the whole reason this is faster than the shelf.
        await Expect(Page.GetByTestId("onboard-staples")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        var suggestions = Page.Locator("[data-testid^='onboard-item-']");
        var suggested = await suggestions.CountAsync();
        Assert.That(suggested, Is.GreaterThan(0), "the wizard suggested nothing to start from");
        await Expect(suggestions.First).ToBeCheckedAsync();

        // Untick one. Its label carries the click, the same as every pill in the app.
        var firstId = await suggestions.First.GetAttributeAsync("id");
        await Page.Locator($"label[for='{firstId}']").ClickAsync();
        await Expect(suggestions.First).Not.ToBeCheckedAsync();

        await Page.GetByTestId("onboard-next").ClickAsync();
        await Expect(Page.GetByTestId("onboard-finish")).ToBeVisibleAsync();

        // Not one request per tick: the whole shelf goes in a single PUT, so it either arrives as the
        // wizard left it or not at all.
        await Page.RunAndWaitForResponseAsync(
            () => Page.GetByTestId("onboard-finish").ClickAsync(),
            r => r.Url.EndsWith("/api/inventory") && r.Request.Method == "PUT" && r.Status == 200);

        // FEATURES §7 step 5: land on what you can make, with the filter already on rather than on
        // the whole catalog.
        await Expect(Page).ToHaveURLAsync(new Regex(@"/cocktails\?makeable=true$"), new() { Timeout = 30_000 });
        await Expect(Page.GetByTestId("cocktail-list").Or(Page.GetByTestId("cocktail-empty")))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // The shelf is the proof the write landed, and that it landed as CONFIRMED: one fewer than
        // was suggested, because one was unticked before Finish.
        await Page.GetByTestId("nav-shelf").ClickAsync();
        await Expect(Page.GetByTestId("shelf-count"))
            .ToContainTextAsync($"{suggested - 1} ", new() { Timeout = 30_000 });
    }
}
