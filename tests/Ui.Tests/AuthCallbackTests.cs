using System.Net;
using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// The auth callback is where every web sign-in lands — OTP, magic link and OAuth all set the refresh
/// cookie and then navigate here for the exchange. It had no test of its own, and BACKBAR-8's restyle
/// replaced the whole file including the <c>@code</c> block, so the page rendered its "Processing
/// sign-in…" line and did nothing else: 45 of 51 browser journeys failed at the same spot, and nothing
/// short of running them said so. These hold the three things the page has to do.
/// </summary>
public class AuthCallbackTests : ComponentTestBase
{
    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public async Task SignedIn_LandsOnHome()
    {
        await SignInAsync();

        // Start ON the callback route, or "landed on home" is just where the fake navigator begins.
        Nav.NavigateTo("/auth-callback");
        var page = Render<AuthCallback>();

        page.WaitForAssertion(() => Assert.Equal("http://localhost/", Nav.Uri));
    }

    [Fact]
    public void NotSignedIn_AndTheRefreshFails_LandsOnTheErrorPage()
    {
        Http.On(HttpMethod.Post, "/api/auth/refresh", "{}", HttpStatusCode.Unauthorized);

        // Start ON the callback route, or "landed on home" is just where the fake navigator begins.
        Nav.NavigateTo("/auth-callback");
        var page = Render<AuthCallback>();

        page.WaitForAssertion(() => Assert.EndsWith("/auth-error", Nav.Uri));
    }

    [Fact]
    public async Task APendingRedirect_IsHonoured_WhenItIsSameOrigin()
    {
        await SignInAsync();
        JSInterop.Setup<string>("localStorage.getItem", "post_login_redirect").SetResult("/join?token=abc");

        // Start ON the callback route, or "landed on home" is just where the fake navigator begins.
        Nav.NavigateTo("/auth-callback");
        var page = Render<AuthCallback>();

        page.WaitForAssertion(() => Assert.EndsWith("/join?token=abc", Nav.Uri));
        JSInterop.VerifyInvoke("localStorage.removeItem");
    }

    [Fact]
    public async Task APendingRedirect_ToAnotherOrigin_IsIgnored()
    {
        // "//evil.example" is a protocol-relative URL — it would leave the site. Home instead.
        await SignInAsync();
        JSInterop.Setup<string>("localStorage.getItem", "post_login_redirect").SetResult("//evil.example/x");

        // Start ON the callback route, or "landed on home" is just where the fake navigator begins.
        Nav.NavigateTo("/auth-callback");
        var page = Render<AuthCallback>();

        page.WaitForAssertion(() => Assert.Equal("http://localhost/", Nav.Uri));
    }
}
