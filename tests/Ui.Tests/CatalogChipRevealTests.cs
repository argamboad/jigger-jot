using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// On a phone the catalog's chip row scrolls sideways (BACKBAR-4 F7), and at the Android app's 448px the
/// selected chip — "Everything", the default — sat past the edge, cut in half (2026-09-16). The row keeps
/// scrolling; the chip that is selected is brought into view whenever the list loads under a scope.
/// </summary>
public class CatalogChipRevealTests : ComponentTestBase
{
    // wwwroot/js/ui.js — scrolls "nearest" on both axes, so the sticky row moves and the page does not.
    private const string Reveal = "appUi.revealChip";

    private int Reveals => JSInterop.Invocations.Count(i => i.Identifier == Reveal);

    private IRenderedComponent<Cocktails> RenderCatalog()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/cocktails");
        Http.On(HttpMethod.Get, "/api/cocktails", """{"items":[],"page":1,"pageSize":20,"total":0}""");
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", "[]");
        Http.On(HttpMethod.Get, "/api/cocktails/starters", "[]");
        return Render<Cocktails>();
    }

    [Fact]
    public void TheSelectedChip_IsBroughtIntoView_OnLoad_AndWhenTheScopeChanges()
    {
        var page = RenderCatalog();
        // Also once the list is in, because the selected chip grows when its count arrives — revealing
        // only as the page appeared left it half past the edge on the device. (Here the stubbed load
        // finishes before the first render, so the two can land as one.)
        page.WaitForAssertion(() => Assert.Contains(" · 0", page.Find("label[for='catalog-all']").TextContent));
        page.WaitForAssertion(() => Assert.True(Reveals >= 1));
        var onLoad = Reveals;

        page.Find("[data-testid='cocktail-makeable']").Change(true);

        page.WaitForAssertion(() => Assert.True(Reveals > onLoad));
    }
}
