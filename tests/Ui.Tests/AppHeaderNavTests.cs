using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Components;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// SHELL-1: the app's three destinations, in the header bar at <c>lg</c> and above and in a bottom tab
/// bar below it.
/// <para>
/// These are structural rather than visual, because the risk in this slice is structural. The obvious
/// way to build a responsive shell is to render the destinations twice and hide one set with CSS —
/// which puts two <c>nav-shelf</c> in the DOM and fails every browser journey that clicks it by test
/// id on an ambiguous locator. A viewport cannot be asserted here; the number of elements can, and
/// that is the thing that actually breaks.
/// </para>
/// </summary>
public class AppHeaderNavTests : ComponentTestBase
{
    private async Task<IRenderedComponent<AppHeader>> HeaderAtAsync(string url)
    {
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/admin/me", """{"isStaff":false}""");
        Http.On(HttpMethod.Get, "/api/notifications/unread-count", """{"count":0}""");
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<AppHeader>();
    }

    [Fact]
    public async Task EachDestinationRendersExactlyOnce()
    {
        var header = await HeaderAtAsync("/shelf");

        // The single highest-risk detail in the epic, asserted directly. One element per destination
        // at any width, because there IS only one — CSS moves it rather than a second copy appearing.
        foreach (var id in new[] { "nav-home", "nav-shelf", "nav-cocktails" })
        {
            Assert.Single(header.FindAll($"[data-testid='{id}']"));
        }
    }

    [Fact]
    public async Task TheDestinationsSitOutsideTheCollapsibleMenu()
    {
        var header = await HeaderAtAsync("/shelf");

        // Inside the collapse, the tab bar would be display:none below lg until someone opened the
        // hamburger — which is exactly the trip through a menu this slice removes. The structure is
        // what makes the slice work, so it is held rather than left to a future tidy-up.
        Assert.Empty(header.FindAll(".navbar-collapse [data-testid='nav-shelf']"));
        Assert.Single(header.FindAll(".app-tabs [data-testid='nav-shelf']"));
    }

    [Fact]
    public async Task TheCurrentDestinationIsAnnounced_NotOnlyStyled()
    {
        var header = await HeaderAtAsync("/shelf");

        // NavLink supplies the `active` CLASS and nothing else, and a tab bar whose current tab is
        // conveyed by styling alone tells a screen reader nothing.
        Assert.Equal("page", header.Find("[data-testid='nav-shelf']").GetAttribute("aria-current"));
        Assert.Null(header.Find("[data-testid='nav-cocktails']").GetAttribute("aria-current"));
        Assert.Null(header.Find("[data-testid='nav-home']").GetAttribute("aria-current"));
    }

    [Fact]
    public async Task ANestedRouteKeepsItsSectionCurrent()
    {
        var header = await HeaderAtAsync("/cocktails/11111111-1111-1111-1111-111111111111");

        // Reading a recipe is still being in the catalog. Matching on the exact path would leave every
        // tab unmarked on the screen someone spends the longest on.
        Assert.Equal("page", header.Find("[data-testid='nav-cocktails']").GetAttribute("aria-current"));
        Assert.Null(header.Find("[data-testid='nav-home']").GetAttribute("aria-current"));
    }

    [Fact]
    public async Task HomeIsCurrentOnlyAtTheRoot()
    {
        var atHome = await HeaderAtAsync("/");
        Assert.Equal("page", atHome.Find("[data-testid='nav-home']").GetAttribute("aria-current"));

        // ...and never anywhere else: "/" is a prefix of every route in the app, so the prefix rule
        // the other two use would light Home up on every screen.
        var atShelf = await HeaderAtAsync("/shelf");
        Assert.Null(atShelf.Find("[data-testid='nav-home']").GetAttribute("aria-current"));
    }

    [Fact]
    public async Task TheCurrentDestinationFollowsNavigation()
    {
        var header = await HeaderAtAsync("/shelf");
        Assert.Equal("page", header.Find("[data-testid='nav-shelf']").GetAttribute("aria-current"));

        Services.GetRequiredService<NavigationManager>().NavigateTo("/cocktails");

        // NavLink refreshes its own class without telling its parent, so the header has to re-render
        // on every location change rather than only when it has a menu to close. Without that it
        // announces the previous page for the rest of the session.
        header.WaitForAssertion(() =>
        {
            Assert.Equal("page", header.Find("[data-testid='nav-cocktails']").GetAttribute("aria-current"));
            Assert.Null(header.Find("[data-testid='nav-shelf']").GetAttribute("aria-current"));
        });
    }
}
