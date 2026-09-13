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
/// BACKBAR-5 (handoff pages 05–06, 13–14): Login and Welcome are her two full appearances — the scene
/// is half the screen and her sentence is the page's headline. Join and the auth error page reuse the
/// same split. Every id survives; nothing is a card.
/// </summary>
public class AnonymousLayoutTests : ComponentTestBase
{
    private const string Gin = "11111111-1111-1111-1111-111111111111";
    private const string Rum = "22222222-2222-2222-2222-222222222222";

    private const string Catalog = $$"""
        [
          {"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":false,"isOwn":false},
          {"id":"{{Rum}}","name":"White rum","category":"Rum","subcategory":null,"isAvailable":false,"isOwn":false}
        ]
        """;

    private const string Staples = $$"""
        [{"ingredientId":"{{Gin}}","ingredient":"London dry gin","appears":40}]
        """;

    [Fact]
    public void Login_IsHerScreen_WithTheFormBeside()
    {
        var page = Render<Login>();

        // The scene is half the screen, her line is its headline, the lockup sits over it; the
        // form side keeps the light lockup (dark-swapped in app.css) and every id.
        var split = page.Find(".split");
        Assert.Contains("Marga_LoginLine", split.QuerySelector(".split-line")!.TextContent);
        Assert.Contains("Onboard_MargaAside", split.QuerySelector(".split-eyebrow")!.TextContent);
        Assert.NotNull(split.QuerySelector(".split-scene-lockup"));
        Assert.NotNull(page.Find(".split-panel [data-testid='login-email']"));
        Assert.NotNull(page.Find(".split-panel img.login-lockup"));
        Assert.NotNull(page.Find(".split-panel h1.page-title"));
        Assert.Empty(page.FindAll(".card"));
        Assert.Empty(page.FindAll("[data-testid='marga']"));   // she is the scene here, not an avatar
    }

    [Fact]
    public async Task Login_CodeStep_SwapsTheButtonsForTheField_AndSheDoesNotMove()
    {
        Http.On(HttpMethod.Post, "/api/auth/otp/send", "{}");
        var page = Render<Login>();
        page.Find("[data-testid=login-email]").Input("ada@example.com");
        await page.Find("[data-testid=login-send-otp]").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            var code = page.Find(".split-panel [data-testid='login-otp-code']");
            Assert.Contains("login-code", code.ClassList);
            Assert.Empty(page.FindAll("[data-testid='login-send-otp']"));
            Assert.Contains("Marga_LoginLine", page.Find(".split-line").TextContent);
        });
    }

    [Fact]
    public void Welcome_IsHerSecondFullAppearance_WithAProgressRule()
    {
        Http.On(HttpMethod.Get, "/api/inventory", Catalog);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", Staples);
        var page = Render<Welcome>();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Marga_OnboardStaples", page.Find(".split-line").TextContent);
            Assert.Contains("Onboard_MargaAside", page.Find(".split-eyebrow").TextContent);
            Assert.Empty(page.FindAll(".card"));
            Assert.NotNull(page.Find(".split-panel [data-testid='onboard-staples']"));
            Assert.NotNull(page.Find(".split-panel h1.page-title"));

            // Step 1 of 2: the copy string is unchanged; the 3px brass rule at 50% is the addition.
            Assert.Contains("Onboard_Step[1, 2]", page.Find("[data-testid='onboard-step']").TextContent);
            Assert.Contains("width: 50%", page.Find(".onboard-progress-fill").GetAttribute("style"));

            // The running total is the same serif-and-copper figure the shelf's payoff uses.
            Assert.Contains("font-display", page.Find("[data-testid='onboard-count']").ClassList);
        });
    }

    [Fact]
    public void Welcome_StepTwo_RendersTheShelfsSections()
    {
        Http.On(HttpMethod.Get, "/api/inventory", Catalog);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", Staples);
        var page = Render<Welcome>();
        page.WaitForAssertion(() => page.Find("[data-testid='onboard-next']"));

        page.Find("[data-testid='onboard-next']").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Marga_OnboardRest", page.Find(".split-line").TextContent);
            Assert.Contains("width: 100%", page.Find(".onboard-progress-fill").GetAttribute("style"));

            // One control, two screens (ONBOARD-1): the shelf's sections verbatim — a serif heading on
            // a hairline with its count — so if the shelf's section changes, this changes with it.
            var sections = page.FindAll(".split-panel section.shelf-section");
            Assert.Equal(2, sections.Count);
            Assert.NotNull(sections[0].QuerySelector("h2.shelf-section-title.font-display"));
            Assert.NotNull(sections[0].QuerySelector(".shelf-section-count"));
            Assert.NotNull(page.Find($"[data-testid='onboard-item-{Gin}']"));
        });
    }

    [Fact]
    public async Task Join_ReusesTheSplit_AndKeepsItsStates()
    {
        StubFeatures(billing: false);
        await SignInAsync();
        Http.On(HttpMethod.Post, "/api/household/invitations/accept", "{}", HttpStatusCode.PaymentRequired);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/join?token=any-token");

        var page = Render<Join>();

        page.WaitForAssertion(() =>
        {
            Assert.NotNull(page.Find(".split-panel [data-testid='join-household-full']"));
            Assert.NotNull(page.Find(".split-scene img.split-scene-art"));
            Assert.Empty(page.FindAll(".card"));

            // BACKBAR-9: the panel's heading is the same serif h1 Login and the auth error use, not the
            // bold sans the split's borrowers kept — the sweep found Join was the one screen still in it.
            var heading = page.Find(".split-panel h1");
            Assert.Contains("page-title", heading.ClassList);
            Assert.DoesNotContain("fw-bold", heading.ClassList);
        });
    }

    [Fact]
    public void AuthError_ReusesTheSplit_WithHerLineReplacedByTheErrorCopy()
    {
        var page = Render<AuthError>();

        Assert.Contains("AuthError_Heading", page.Find(".split-line").TextContent);
        Assert.Empty(page.FindAll(".split-eyebrow"));   // nothing to attribute — it is not her line
        Assert.Contains("AuthError_Body", page.Find(".split-panel").TextContent);
        Assert.NotNull(page.Find(".split-panel a[href='/login']"));
    }
}
