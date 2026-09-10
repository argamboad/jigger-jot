using Bunit;
using Xunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// Proves the component-test chassis (v3 TOOL-2): renders the REAL <see cref="Home"/> page against the
/// doubles and asserts both auth branches. If this passes, the chassis correctly fakes localization + drives
/// AuthService signed-in/anonymous state — the foundation the client-UX slices (T39–T42) build on.
/// MARGA-2 replaced the inherited welcome copy with the answer to the product's question, so the signed-in
/// branch is now asserted through the numbers rather than through a greeting.
/// </summary>
public class HomePageTests : ComponentTestBase
{
    private const string TwoMakeable = """
        {"items":[
            {"id":"11111111-1111-1111-1111-111111111111","name":"Negroni","glass":"Rocks","method":"Stir",
             "servingType":"OnTheRocks","source":"IBA","isOwn":false,"ingredientCount":3,
             "substitutions":[],"missingIngredient":null},
            {"id":"22222222-2222-2222-2222-222222222222","name":"Americano","glass":null,"method":null,
             "servingType":"OnTheRocks","source":"IBA","isOwn":false,"ingredientCount":3,
             "substitutions":[],"missingIngredient":null}],
         "page":1,"pageSize":3,"total":2}
        """;

    private const string OneBottle = """
        [{"ingredientId":"33333333-3333-3333-3333-333333333333","ingredient":"Dry vermouth",
          "unlocks":2,"cocktails":["Martini","Manhattan"]}]
        """;

    [Fact]
    public void Anonymous_ShowsHeroAndSignInCta()
    {
        var cut = Render<Home>();

        // The deterministic localizer renders keys, so we assert on the key the anonymous branch uses.
        Assert.Contains("Home_SignInCta", cut.Markup);
        Assert.Contains("/login", cut.Markup);
        Assert.DoesNotContain("home-marga", cut.Markup);
    }

    [Fact]
    public async Task SignedIn_ShowsTheCount_AndTheBottleThatWouldExtendIt()
    {
        await SignInAsync(name: "Ada Lovelace", tenantName: "Test Household");
        Http.On(HttpMethod.Get, "/api/cocktails", TwoMakeable);
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", OneBottle);

        var cut = Render<Home>();

        cut.WaitForAssertion(() =>
        {
            // The headline count and the drinks under it come from ONE response, so the test asserts both.
            Assert.Equal("2", cut.Find("[data-testid='home-count']").TextContent.Trim());
            Assert.Contains("Negroni", cut.Markup);
            Assert.Contains("Americano", cut.Markup);

            // Marga_HomeBottle is a parameterized key; the fake localizer echoes the arguments, so the two
            // numbers flowing from the API through the component are observable — and each agrees with the
            // list rendered beside it.
            Assert.Contains("Marga_HomeBottle[Dry vermouth, 2]", cut.Markup);
            Assert.Contains("Martini", cut.Markup);

            // Both CTAs are deep links, so each lands on the list that produced the number above it.
            Assert.Equal("/cocktails?makeable=true",
                cut.Find("[data-testid='home-show-makeable']").GetAttribute("href"));
            Assert.Equal("/cocktails?almost=true",
                cut.Find("[data-testid='home-show-almost']").GetAttribute("href"));
        });

        Assert.DoesNotContain("Home_SignInCta", cut.Markup);
    }

    [Fact]
    public async Task SignedIn_WithNothingTicked_PointsAtTheShelf()
    {
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/cocktails", """{"items":[],"page":1,"pageSize":3,"total":0}""");
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", "[]");

        var cut = Render<Home>();

        cut.WaitForAssertion(() =>
        {
            // Nothing to count, so she says the empty line and the one control is the shelf.
            Assert.Contains("Marga_HomeEmpty", cut.Markup);
            Assert.Contains("Make_FillYourShelf", cut.Markup);
            Assert.Equal("/shelf", cut.Find("[data-testid='home-update-shelf']").GetAttribute("href"));
            Assert.Empty(cut.FindAll("[data-testid='home-count']"));
        });
    }

    [Fact]
    public async Task SignInHelper_DrivesTheRealRefreshEndpoint()
    {
        // Guard the chassis's own contract: SignInAsync must reach signed-in state THROUGH the refresh
        // endpoint (AuthService's real code path), not a reflection shortcut.
        await SignInAsync();

        Assert.True(Auth.IsAuthenticated);
        Assert.Contains(Http.Requests, r =>
            r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath == "/api/auth/refresh");
    }
}
