using System.Net;
using System.Net.Http;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// ONBOARD-1, FEATURES §7: the wizard a brand-new household meets instead of an empty catalog.
/// <para>
/// The rule these all turn on is that <b>nothing is written until Finish</b>. The wizard puts a dozen
/// suggestions on screen ALREADY TICKED, which is what makes it fast — and which means writing as it
/// goes would record a shelf the household never confirmed, and leave half of one behind for anyone
/// who closed the tab midway.
/// </para>
/// </summary>
public class WelcomeWizardTests : ComponentTestBase
{
    private const string Gin = "11111111-1111-1111-1111-111111111111";
    private const string Rum = "22222222-2222-2222-2222-222222222222";
    private const string Owned = "33333333-3333-3333-3333-333333333333";

    private const string Catalog = $$"""
        [
          {"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":false,"isOwn":false},
          {"id":"{{Rum}}","name":"White rum","category":"Rum","subcategory":null,"isAvailable":false,"isOwn":false},
          {"id":"{{Owned}}","name":"Angostura","category":"Bitters","subcategory":null,"isAvailable":true,"isOwn":false}
        ]
        """;

    private const string Staples = $$"""
        [{"ingredientId":"{{Gin}}","ingredient":"London dry gin","appears":40}]
        """;

    private IRenderedComponent<Welcome> RenderWizard()
    {
        Http.On(HttpMethod.Get, "/api/inventory", Catalog);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", Staples);
        Http.On(HttpMethod.Put, "/api/inventory", """{"applied":3,"unknown":[]}""");
        return Render<Welcome>();
    }

    /// <summary>The single bulk write, parsed — or null if it was never sent.</summary>
    private Dictionary<string, bool>? Written()
    {
        var index = Http.Requests.FindIndex(r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == "/api/inventory");
        if (index < 0) return null;

        using var body = JsonDocument.Parse(Http.Bodies[index]);
        // Keyed by the id as a STRING so the constants above can be used verbatim: they are the ids
        // the fixture sends, and round-tripping them through Guid.Parse in every assertion adds noise
        // without adding a check.
        return body.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("ingredientId").GetString()!,
                          i => i.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public void TheSuggestionsArriveTicked_AndWhatIsAlreadyOwnedStaysTicked()
    {
        var page = RenderWizard();

        page.WaitForAssertion(() =>
        {
            // Answering "which of these is wrong" is far faster than picking a dozen bottles out of
            // 191, and that only works if they start selected.
            Assert.True(page.Find($"[data-testid='onboard-item-{Gin}']").HasAttribute("checked"));

            // A household that joined by invitation already has a shelf (FEATURES §7), and so does
            // anyone who opens this a second time. Neither should find their answers thrown away.
            Assert.Contains("Shelf_Count[2]", page.Find("[data-testid='onboard-count']").TextContent);
        });
    }

    [Fact]
    public void NothingIsWrittenUntilFinish()
    {
        var page = RenderWizard();
        page.WaitForAssertion(() => page.Find($"[data-testid='onboard-item-{Gin}']"));

        page.Find($"[data-testid='onboard-item-{Gin}']").Change(false);
        page.Find("[data-testid='onboard-next']").Click();

        // Untick a suggestion, walk to the second step — and the server has still heard nothing. A
        // wizard that wrote as it went would have recorded a shelf nobody confirmed.
        Assert.Null(Written());
    }

    [Fact]
    public void Finish_SendsTheWholeShelfInOneRequest()
    {
        var page = RenderWizard();
        page.WaitForAssertion(() => page.Find($"[data-testid='onboard-item-{Gin}']"));

        // Step one shows only the suggestions, so anything else is ticked on step two — which is what
        // the second step is for.
        page.Find("[data-testid='onboard-next']").Click();
        page.Find($"[data-testid='onboard-item-{Rum}']").Change(true);
        page.Find("[data-testid='onboard-finish']").Click();

        page.WaitForAssertion(() => Assert.NotNull(Written()));

        // EVERY row, not just the changes: the wizard's answer is "this is my shelf", and sending
        // only the differences would make the result depend on what the shelf held when it landed.
        var written = Written()!;
        Assert.Equal(3, written.Count);
        Assert.True(written[Gin]);
        Assert.True(written[Rum]);
        Assert.True(written[Owned]);

        // One request, so the shelf either arrives as the wizard left it or not at all.
        Assert.Single(Http.Requests, r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == "/api/inventory");
    }

    [Fact]
    public void AnUntickedSuggestion_IsSentAsUnavailable_NotOmitted()
    {
        var page = RenderWizard();
        page.WaitForAssertion(() => page.Find($"[data-testid='onboard-item-{Gin}']"));

        page.Find($"[data-testid='onboard-item-{Gin}']").Change(false);   // a suggestion, on step one
        page.Find("[data-testid='onboard-next']").Click();
        page.Find($"[data-testid='onboard-item-{Owned}']").Change(false); // already owned, on step two
        page.Find("[data-testid='onboard-finish']").Click();

        page.WaitForAssertion(() => Assert.NotNull(Written()));

        // "I checked and I do not have it" has to reach the server as a false, or unticking something
        // the household already owned would silently leave it on the shelf (INV-1, JJ-023).
        var written = Written()!;
        Assert.False(written[Gin]);
        Assert.False(written[Owned]);
    }

    [Fact]
    public void Finish_LandsOnWhatICanMake()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var page = RenderWizard();
        page.WaitForAssertion(() => page.Find("[data-testid='onboard-next']"));

        page.Find("[data-testid='onboard-next']").Click();
        page.Find("[data-testid='onboard-finish']").Click();

        // FEATURES §7 step 5, and deep-linked the way the home screen's buttons are, so the list
        // arrives with the filter already on rather than showing the whole catalog.
        page.WaitForAssertion(() => Assert.EndsWith("/cocktails?makeable=true", nav.Uri));
    }

    [Fact]
    public void AFailedSave_SaysSo_AndKeepsTheAnswers()
    {
        Http.On(HttpMethod.Get, "/api/inventory", Catalog);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", Staples);
        Http.On(HttpMethod.Put, "/api/inventory", "{}", HttpStatusCode.InternalServerError);
        var nav = Services.GetRequiredService<NavigationManager>();
        var before = nav.Uri;

        var page = Render<Welcome>();
        page.WaitForAssertion(() => page.Find("[data-testid='onboard-next']"));
        page.Find("[data-testid='onboard-next']").Click();
        page.Find("[data-testid='onboard-finish']").Click();

        // Walking someone through a wizard and then dropping their answers on the floor is worse than
        // not offering one: the page stays, the ticks stay, and Finish can be pressed again.
        page.WaitForAssertion(() => page.Find("[data-testid='onboard-save-error']"));
        Assert.Equal(before, nav.Uri);
        Assert.NotNull(page.Find("[data-testid='onboard-finish']"));
    }
}
