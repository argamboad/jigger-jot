using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Components;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-2 (JJ-037): the chrome leaves copper. The bar sits on the surface colour in both themes,
/// so nothing in it may assume a dark ground any more — no <c>navbar-dark</c>, no
/// <c>btn-outline-light</c>, no white-alpha text. These hold the structure; the colours themselves
/// are the browser journey's to see.
/// </summary>
public class AppHeaderChromeTests : ComponentTestBase
{
    private async Task<IRenderedComponent<AppHeader>> HeaderAsync()
    {
        await SignInAsync();
        StubFeatures(billing: true);
        Http.On(HttpMethod.Get, "/api/admin/me", """{"isStaff":false}""");
        Http.On(HttpMethod.Get, "/api/notifications/unread-count", """{"count":0}""");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/shelf");
        return Render<AppHeader>();
    }

    [Fact]
    public async Task TheBarNoLongerAssumesADarkGround()
    {
        var header = await HeaderAsync();
        var nav = header.Find("nav.app-header");

        // navbar-dark hard-codes white text for a dark band. On the surface colour the bar has to
        // follow data-bs-theme like everything else, which Bootstrap 5.3 does when the class is absent.
        Assert.DoesNotContain("navbar-dark", nav.ClassList);
        Assert.DoesNotContain("navbar-light", nav.ClassList);
    }

    [Fact]
    public async Task NothingInTheBarIsPaintedForACopperBand()
    {
        var header = await HeaderAsync();

        // Outline-light buttons are white lines on copper; on a white bar they vanish. Every button in
        // the account cluster takes the same theme-aware variant, so an anchor-shaped button and the
        // <button> beside it can never differ — the very thing the old dark-theme bug produced.
        Assert.Empty(header.FindAll(".btn-outline-light"));
        Assert.Empty(header.FindAll(".text-white-50, .text-white"));

        var signOut = header.Find("[data-testid='sign-out']");
        var billing = header.Find("[data-testid='nav-billing']");
        Assert.Contains("btn-outline-secondary", signOut.ClassList);
        Assert.Contains("btn-outline-secondary", billing.ClassList);
    }

    [Fact]
    public async Task TheMarkHasAVariantForEachGround()
    {
        var header = await HeaderAsync();
        var mark = header.Find("img.brand-icon");

        // The copper mark is the light-ground one; the stylesheet swaps in the bone variant under
        // dark through the same content:url() pattern the lockups use. The markup names the light
        // one so the swap has one direction, and so a host with no theme script still gets a mark.
        Assert.EndsWith("brand/icon_light.svg", mark.GetAttribute("src"));
        Assert.Equal("", mark.GetAttribute("alt"));
    }
}
