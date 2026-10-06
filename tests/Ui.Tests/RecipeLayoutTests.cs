using System.Net;
using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-4, the recipe (handoff pages 09–10): amounts lead, in the display face; a line says
/// something in exactly two cases; an optional line is a dash and plain text, not a badge;
/// provenance sits in the facet line; and not-found is an empty state rather than an alert.
/// </summary>
public class RecipeLayoutTests : ComponentTestBase
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string Martinez = """
        {"id":"11111111-1111-1111-1111-111111111111","name":"Martinez","glass":"Coupe","method":"Stir",
         "servingType":"Straight","instructions":"Stir with ice, strain.",
         "source":null,"isOwn":true,"makeability":"AlmostMakeable",
         "forkedFrom":{"id":"22222222-2222-2222-2222-222222222222","name":"Martinez"},
         "lines":[
           {"ingredient":"Old Tom gin","amount":45,"unit":"ml","display":"45 ml","isRequired":true,"role":"base","notes":null,"availability":"Available","substituteWith":null},
           {"ingredient":"Sweet vermouth","amount":45,"unit":"ml","display":"45 ml","isRequired":true,"role":"modifier","notes":null,"availability":"Substitute","substituteWith":"Punt e Mes"},
           {"ingredient":"Maraschino","amount":1,"unit":"barspoon","display":"1 barspoon","isRequired":true,"role":"modifier","notes":null,"availability":"Missing","substituteWith":null},
           {"ingredient":"Angostura bitters","amount":2,"unit":"dash","display":"2 dashes","isRequired":true,"role":"bitters","notes":null,"availability":"Available","substituteWith":null},
           {"ingredient":"Orange twist","amount":null,"unit":null,"display":"","isRequired":false,"role":"garnish","notes":"expressed over the top","availability":"Missing","substituteWith":null}
         ]}
        """;

    private IRenderedComponent<CocktailDetail> RenderRecipe()
    {
        Http.On(HttpMethod.Get, $"/api/cocktails/{Id}", Martinez);
        return Render<CocktailDetail>(ps => ps.Add(p => p.Id, Id));
    }

    [Fact]
    public void AmountsLead_InTheDisplayFace_AndTheTitleIsTheSerif()
    {
        var page = RenderRecipe();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("font-display", page.Find("[data-testid='cocktail-name']").ClassList);
            var amounts = page.FindAll("[data-testid='cocktail-lines'] li .recipe-amount");
            Assert.Equal(5, amounts.Count);
            Assert.All(amounts, a => Assert.Contains("font-display", a.ClassList));
            Assert.Equal("45 ml", amounts[0].TextContent.Trim());
        });
    }

    [Fact]
    public void ALineSaysSomethingInExactlyTwoCases()
    {
        var page = RenderRecipe();

        page.WaitForAssertion(() =>
        {
            Assert.Single(page.FindAll("[data-testid='cocktail-line-substitute']"));
            // The garnish is missing too, but it is optional — an optional line never blocks and
            // never nags (JJ-009), so only the required Maraschino carries the mark.
            Assert.Single(page.FindAll("[data-testid='cocktail-line-missing']"));
            Assert.Equal(5, page.FindAll("[data-testid='cocktail-lines'] li").Count);
        });
    }

    [Fact]
    public void AnOptionalLineIsADashAndPlainText_NotABadge()
    {
        var page = RenderRecipe();

        page.WaitForAssertion(() =>
        {
            var lines = page.FindAll("[data-testid='cocktail-lines'] li");
            var garnish = lines[4];
            Assert.Equal("—", garnish.QuerySelector(".recipe-amount")!.TextContent.Trim());
            Assert.Contains("Cocktail_Optional", garnish.TextContent);
            Assert.Empty(page.FindAll("[data-testid='cocktail-lines'] .badge"));
        });
    }

    [Fact]
    public void ProvenanceSitsInTheFacetLine_AndNothingIsBoxed()
    {
        var page = RenderRecipe();

        page.WaitForAssertion(() =>
        {
            var facets = page.Find("[data-testid='cocktail-facets']");
            Assert.NotNull(facets.QuerySelector("[data-testid='cocktail-forked-from']"));
            Assert.Contains("Cocktail_BasedOn", facets.TextContent);
            Assert.Empty(page.FindAll(".card"));
            Assert.NotNull(page.Find("[data-testid='cocktail-marga'] .marga-card"));
        });
    }

    [Fact]
    public void NotFoundIsAnEmptyState_NotAnAlert()
    {
        Http.On(HttpMethod.Get, $"/api/cocktails/{Id}", "", HttpStatusCode.NotFound);
        var page = Render<CocktailDetail>(ps => ps.Add(p => p.Id, Id));

        page.WaitForAssertion(() =>
        {
            var notFound = page.Find("[data-testid='cocktail-notfound']");
            Assert.DoesNotContain("alert", notFound.ClassList);
            Assert.NotNull(notFound.QuerySelector("img.empty-scene"));
            Assert.Contains("Cocktail_NotFound", notFound.TextContent);
            Assert.NotNull(notFound.QuerySelector("a[href='/cocktails']"));
        });
    }
}
