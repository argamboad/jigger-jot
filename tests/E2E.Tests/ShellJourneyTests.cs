using Microsoft.Playwright;

namespace JiggerJot.E2E.Tests;

/// <summary>
/// SHELL-1: the app's three destinations follow the width — in the header bar at <c>lg</c> and above,
/// in a bottom tab bar below it.
/// <para>
/// The point of this journey is the test ids, which every other journey depends on. It clicks them at
/// a phone width, and the click is itself the assertion: Playwright refuses an ambiguous locator, so
/// a second copy of the destinations rendered for narrow screens and hidden with CSS would fail here
/// rather than in whichever unrelated suite happened to run first.
/// </para>
/// </summary>
[TestFixture]
public class ShellJourneyTests : E2ETestBase
{
    [Test]
    public async Task TheDestinationsFollowTheWidth_AndKeepTheirTestIds()
    {
        await Mailpit.ClearAsync();
        await SignInAsync(Page, UniqueEmail("shell"));

        // Wide first: the destinations live in the header, beside the brand.
        await Page.SetViewportSizeAsync(1280, 800);
        await Expect(Page.GetByTestId("nav-shelf")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        // The current tab's WEIGHT, asserted before anything else, because it is the cheapest proof
        // that these rules reach the element at all. They were authored in AppHeader.razor.css, and
        // Blazor's CSS isolation stamps its scope attribute only on the component's OWN markup — the
        // anchor here is rendered by <NavLink>, a child component, so every rule compiled to a
        // selector matching NOTHING. It shipped that way: the destinations kept Bootstrap's colour
        // and no tab was ever bold. A screenshot would have shown it; no test could.
        await Expect(Page.GetByTestId("nav-home")).ToHaveCSSAsync("font-weight", "700");
        await Expect(Page.GetByTestId("nav-shelf")).ToHaveCSSAsync("font-weight", "500");

        // ...and in DARK theme the hierarchy must hold. Two invariants, stated as relations rather
        // than as literal colours (BACKBAR-2 / JJ-037 moved the bar off copper, and the literals went
        // with it): a destination is painted in the page's own ink, never fainter than the account
        // cluster beside it; and an anchor-shaped button takes exactly the colour of the <button>
        // next to it. The second is the old bug — `[data-bs-theme="dark"] a` at 0,1,1 outranked
        // Bootstrap's 0,1,0 button colour and repainted every anchor-shaped button copper while the
        // identical <button> stayed put. "Sign out" is that button, so it is the control.
        await Page.GetByTestId("theme-switcher").SelectOptionAsync("dark");
        var ink = await Page.EvaluateAsync<string>("getComputedStyle(document.body).color");
        await Expect(Page.GetByTestId("nav-shelf")).ToHaveCSSAsync("color", ink);
        var buttonColour = await Page.GetByTestId("sign-out").EvaluateAsync<string>("el => getComputedStyle(el).color");
        await Expect(Page.GetByTestId("nav-billing")).ToHaveCSSAsync("color", buttonColour);
        var linkColour = await Page.EvaluateAsync<string>(
            "getComputedStyle(document.documentElement).getPropertyValue('--bs-link-color').trim()");
        Assert.That(buttonColour, Is.Not.EqualTo(linkColour), "an anchor-shaped button took the link colour");

        // A long household name must never push "Sign out" off the end of the bar, and the page must
        // never scroll sideways to reach it.
        Assert.That(await Page.EvaluateAsync<bool>(
            "document.body.scrollWidth > document.documentElement.clientWidth"), Is.False,
            "the header overflowed and the page scrolled sideways");
        await Page.GetByTestId("nav-cocktails").ClickAsync();
        await Expect(Page.GetByTestId("cocktail-list").Or(Page.GetByTestId("cocktail-empty")))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // ...and the same three at a phone width, without opening anything. Reaching the shelf
        // through a hamburger menu is the trip this slice removes.
        await Page.SetViewportSizeAsync(390, 844);
        var shelf = Page.GetByTestId("nav-shelf");
        await Expect(shelf).ToBeVisibleAsync();
        await shelf.ClickAsync();
        await Expect(Page.GetByTestId("shelf-search")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        // The tab bar is anchored to the bottom of the viewport rather than to the end of the page,
        // so it is on screen without scrolling — which is the whole reason for putting it there.
        var box = await shelf.BoundingBoxAsync();
        Assert.That(box, Is.Not.Null);
        Assert.That(box!.Y, Is.GreaterThan(844 / 2.0), "the tab bar should sit in the lower half of the viewport");

        // The current destination is announced, not merely styled.
        await Expect(shelf).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Page.GetByTestId("nav-cocktails")).Not.ToHaveAttributeAsync("aria-current", "page");

        // Nothing else anchored to the bottom hides under it. INV-3's payoff footer is the first
        // claimant and will not be the last, so the clearance is asserted as geometry rather than as
        // "the element is visible" — a footer sitting exactly behind the tab bar is visible too.
        var payoff = await Page.GetByTestId("shelf-payoff").BoundingBoxAsync();
        Assert.That(payoff, Is.Not.Null);
        Assert.That(payoff!.Y + payoff.Height, Is.LessThanOrEqualTo(box.Y + 1),
            "the payoff footer should end at or above the top of the tab bar");
    }
}
