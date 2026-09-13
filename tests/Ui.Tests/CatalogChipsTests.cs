using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-4, the catalog (handoff pages 07–08): the two switches become one exclusive chip row of
/// three RADIOS — I can make now / One ingredient away / Everything — because they already behaved
/// exclusively in code (JJ-036). Only the selected chip carries a count, the response's own total,
/// so no new fetch. Rows are hairlines with Marga inline; the only panel is the unlocks one.
/// </summary>
public class CatalogChipsTests : ComponentTestBase
{
    private const string Page = """
        {"items":[
            {"id":"11111111-1111-1111-1111-111111111111","name":"White Lady","glass":"Coupe","method":"Shake",
             "servingType":"Straight","source":"IBA","isOwn":false,"ingredientCount":3,
             "substitutions":[{"asksFor":"Cointreau","youHave":"Curaçao"}],"missingIngredient":null},
            {"id":"22222222-2222-2222-2222-222222222222","name":"Negroni","glass":"Rocks","method":"Stir",
             "servingType":"OnTheRocks","source":"The Savoy Cocktail Book","isOwn":true,"ingredientCount":3,
             "substitutions":[],"missingIngredient":"Sweet vermouth"}],
         "page":1,"pageSize":20,"total":14}
        """;

    private const string OneBottle = """
        [{"ingredientId":"33333333-3333-3333-3333-333333333333","ingredient":"Sweet vermouth",
          "unlocks":6,"cocktails":["Negroni","Manhattan","Rob Roy","Americano","Martinez","Bobby Burns"]}]
        """;

    private IRenderedComponent<Cocktails> RenderAt(string url)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        Http.On(HttpMethod.Get, "/api/cocktails", Page);
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", OneBottle);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", "[]");
        return Render<Cocktails>();
    }

    [Fact]
    public void TheChipsAreOneRadioGroup_WithExactlyOneChecked()
    {
        var page = RenderAt("/cocktails");

        page.WaitForAssertion(() =>
        {
            var makeable = page.Find("[data-testid='cocktail-makeable']");
            var almost = page.Find("[data-testid='cocktail-almost']");
            var all = page.Find("[data-testid='cocktail-all']");

            foreach (var input in new[] { makeable, almost, all })
            {
                Assert.Equal("radio", input.GetAttribute("type"));
                Assert.Contains("btn-check", input.ClassList);
                Assert.Equal("catalog-scope", input.GetAttribute("name"));
            }

            // A plain browse is "Everything": the third chip is the state the two switches used to
            // leave implicit, and a radio group with nothing checked would be a control with no answer.
            Assert.True(all.HasAttribute("checked"));
            Assert.False(makeable.HasAttribute("checked"));
            Assert.False(almost.HasAttribute("checked"));
        });
    }

    [Fact]
    public void ADeepLinkPreselectsItsChip()
    {
        var page = RenderAt("/cocktails?almost=true");

        page.WaitForAssertion(() =>
        {
            Assert.True(page.Find("[data-testid='cocktail-almost']").HasAttribute("checked"));
            Assert.False(page.Find("[data-testid='cocktail-all']").HasAttribute("checked"));
        });
    }

    [Fact]
    public void OnlyTheSelectedChipCarriesTheCount()
    {
        var page = RenderAt("/cocktails?makeable=true");

        page.WaitForAssertion(() =>
        {
            // The response's own total, on the chip that produced it; the others say nothing, because
            // saying something would cost a request each and the pager already knows this number.
            Assert.Contains("14", page.Find("label[for='catalog-makeable']").TextContent);
            Assert.DoesNotMatch(@"\d", page.Find("label[for='catalog-almost']").TextContent);
            Assert.DoesNotMatch(@"\d", page.Find("label[for='catalog-all']").TextContent);

            // ...and it still issued exactly one GET for the list.
            Assert.Single(Http.Requests, r => r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath == "/api/cocktails");
        });
    }

    [Fact]
    public void RowsAreHairlines_WithMargaInline_AndTheOnlyPanelIsHers()
    {
        var page = RenderAt("/cocktails?almost=true");

        page.WaitForAssertion(() =>
        {
            // The rows keep their list-group classes (the browse journey reads them) and lose the box.
            Assert.NotNull(page.Find("[data-testid='cocktail-list'] .list-group-item"));
            var panels = page.FindAll(".card");
            Assert.Single(panels);
            Assert.Equal("cocktail-unlocks", panels[0].GetAttribute("data-testid"));

            // Inline in a row, 32px, no label; the compact 24px footnote is gone.
            Assert.NotNull(page.Find("[data-testid='cocktail-substitution'] .marga-inline"));
            Assert.Empty(page.FindAll(".marga-compact"));

            // The missing-ingredient line is on the row and still names the bottle.
            Assert.Contains("Sweet vermouth", page.Find("[data-testid='cocktail-missing']").TextContent);

            // The unlocks panel is her card tone.
            Assert.NotNull(page.Find("[data-testid='cocktail-unlocks'] .marga-card"));
        });
    }

    [Fact]
    public void UnderMakeable_SheSaysTheCount_AndTheIdStaysWhereTheNumberIs()
    {
        var page = RenderAt("/cocktails?makeable=true");

        page.WaitForAssertion(() =>
        {
            var count = page.Find("[data-testid='cocktail-count']");
            Assert.Contains("Marga_MakeableCount[14]", count.TextContent);
            Assert.NotNull(count.QuerySelector(".marga-card"));
            Assert.Empty(page.FindAll(".card"));
        });
    }
}
