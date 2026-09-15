using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-3, Home (handoff pages 03–04): rules, not cards. The only boxed thing on the screen is the
/// unlock panel, so it reads as the one thing to act on; the count is in the display face; Marga is
/// the page's voice at 96px; and an empty shelf gets her scene, never a zero.
/// </summary>
public class HomeLayoutTests : ComponentTestBase
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
    public async Task TheUnlockPanelIsTheOnlyBoxOnThePage()
    {
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/cocktails", TwoMakeable);
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", OneBottle);

        var cut = Render<Home>();

        cut.WaitForAssertion(() =>
        {
            var panels = cut.FindAll(".card");
            Assert.Single(panels);
            Assert.Equal("home-unlocks", panels[0].GetAttribute("data-testid"));

            // The count IS the headline, so it is set in the display face; the drinks under it are
            // hairline rules rather than a list-group.
            Assert.Contains("font-display", cut.Find("[data-testid='home-count']").ClassList);
            Assert.DoesNotContain("list-group", cut.Find("[data-testid='home-makeable-list']").ClassList);

            // She is the page's voice here: the card tone, label above the line.
            Assert.NotNull(cut.Find("[data-testid='home-marga'] .marga-card"));
            Assert.Contains("Marga_HomeAside", cut.Find("[data-testid='home-marga'] .marga-label").TextContent);
        });
    }

    [Fact]
    public async Task AnEmptyShelfGetsHerScene_AndNoZero()
    {
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/cocktails", """{"items":[],"page":1,"pageSize":3,"total":0}""");
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", "[]");

        var cut = Render<Home>();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='home-count']"));
            Assert.Empty(cut.FindAll(".card"));
            Assert.NotNull(cut.Find("[data-testid='home-marga'] img.home-scene"));
            Assert.Contains("Marga_HomeEmpty", cut.Find("[data-testid='home-marga']").TextContent);
            Assert.Contains("btn-primary", cut.Find("[data-testid='home-setup-shelf']").ClassList);
            Assert.Contains("btn-link", cut.Find("[data-testid='home-update-shelf']").ClassList);
        });
    }
}
