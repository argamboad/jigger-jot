using System.Net.Http;
using System.Web;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// AUTHORING-3: picking a line's ingredient fills in its role — and whether it is required — from what
/// the ingredient is. The rule is the server's (<c>GET /api/cocktails/roles</c>); what is tested here is
/// that the form asks, applies the answer, and never overwrites a choice someone made by hand.
/// </summary>
public class WriteRoleSuggestionTests : ComponentTestBase
{
    private const string Gin = "44444444-4444-4444-4444-444444444444";
    private const string Lime = "55555555-5555-5555-5555-555555555555";
    private const string Mint = "66666666-6666-6666-6666-666666666666";

    private const string Lookups = """
        {"glasses":[],"methods":[],
         "units":[{"id":"33333333-3333-3333-3333-333333333333","name":"oz"}],
         "servingTypes":["FullDrink","Shot"],
         "roles":["Base","Modifier","Juice","Syrup","Bitters","Garnish","Mixer","Other"]}
        """;

    private const string Inventory = """
        [{"id":"44444444-4444-4444-4444-444444444444","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":true,"isOwn":false},
         {"id":"55555555-5555-5555-5555-555555555555","name":"Lime juice","category":"Juice","subcategory":null,"isAvailable":true,"isOwn":false},
         {"id":"66666666-6666-6666-6666-666666666666","name":"Mint","category":"Herb and spice","subcategory":null,"isAvailable":true,"isOwn":false}]
        """;

    private static readonly Dictionary<string, (string Role, bool Required)> ServerRule = new(StringComparer.OrdinalIgnoreCase)
    {
        [Gin] = ("Base", true),
        [Lime] = ("Juice", true),
        [Mint] = ("Garnish", false),
    };

    private IRenderedComponent<WriteCocktail> RenderForm()
    {
        Http.On(HttpMethod.Get, "/api/cocktails/lookups", Lookups);
        Http.On(HttpMethod.Get, "/api/inventory", Inventory);
        // Answers what was asked, in the order it was asked — the contract the form relies on.
        Http.On(HttpMethod.Get, "/api/cocktails/roles", request =>
        {
            var ids = HttpUtility.ParseQueryString(request.RequestUri!.Query).GetValues("ingredient") ?? [];
            return "[" + string.Join(",", ids.Select(id =>
                $$"""{"ingredientId":"{{id}}","role":"{{ServerRule[id].Role}}","isRequired":{{(ServerRule[id].Required ? "true" : "false")}}}""")) + "]";
        });

        var page = Render<WriteCocktail>();
        page.WaitForAssertion(() => page.Find("[data-testid='new-name']"));
        return page;
    }

    private static string RoleOf(IRenderedComponent<WriteCocktail> page, int line) =>
        page.FindAll("[data-testid='new-line-role']")[line].GetAttribute("value") ?? "";

    private static bool RequiredOf(IRenderedComponent<WriteCocktail> page, int line) =>
        page.FindAll("[data-testid='new-line-required']")[line].HasAttribute("checked");

    [Fact]
    public void PickingAnIngredient_FillsInItsRole()
    {
        var page = RenderForm();

        page.Find("[data-testid='new-line-ingredient']").Change(Lime);

        page.WaitForAssertion(() => Assert.Equal("Juice", RoleOf(page, 0)));
        Assert.True(RequiredOf(page, 0));
    }

    [Fact]
    public void AGarnish_IsSuggestedAsOptional()
    {
        var page = RenderForm();
        Assert.True(RequiredOf(page, 0));

        page.Find("[data-testid='new-line-ingredient']").Change(Mint);

        // Optional lines never block makeability (JJ-009), so a sprig of mint must not arrive required.
        page.WaitForAssertion(() =>
        {
            Assert.Equal("Garnish", RoleOf(page, 0));
            Assert.False(RequiredOf(page, 0));
        });
    }

    [Fact]
    public void ARoleChosenByHand_IsNeverOverwritten()
    {
        var page = RenderForm();

        page.Find("[data-testid='new-line-role']").Change("Modifier");
        page.Find("[data-testid='new-line-ingredient']").Change(Lime);

        // The question was still asked — the suggestion simply does not win over a person.
        page.WaitForAssertion(() => Assert.Contains(Http.Requests, r => r.RequestUri!.AbsolutePath == "/api/cocktails/roles"));
        Assert.Equal("Modifier", RoleOf(page, 0));
    }

    [Fact]
    public void ARequiredBoxChangedByHand_IsNeverOverwritten()
    {
        var page = RenderForm();

        page.Find("[data-testid='new-line-required']").Change(false);
        page.Find("[data-testid='new-line-ingredient']").Change(Lime);

        page.WaitForAssertion(() => Assert.Equal("Juice", RoleOf(page, 0)));
        Assert.False(RequiredOf(page, 0));
    }

    [Fact]
    public async Task TheWholeRecipeIsAsked_InOrder_BecauseOnlyTheFirstSpiritLeads()
    {
        var page = RenderForm();

        await page.Find("[data-testid='new-line-add']").ClickAsync(new());
        page.FindAll("[data-testid='new-line-ingredient']")[1].Change(Gin);
        page.FindAll("[data-testid='new-line-ingredient']")[0].Change(Lime);

        page.WaitForAssertion(() =>
        {
            var last = Http.Requests.Last(r => r.RequestUri!.AbsolutePath == "/api/cocktails/roles");
            var asked = HttpUtility.ParseQueryString(last.RequestUri!.Query).GetValues("ingredient");
            Assert.Equal([Lime, Gin], asked!);
            Assert.Equal("Juice", RoleOf(page, 0));
            Assert.Equal("Base", RoleOf(page, 1));
        });
    }
}
