using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-3, the shelf (handoff pages 11–12): cards become sections. Each category is a serif
/// heading on a hairline with its count right-aligned, the anchors survive, the bottle Marga names is
/// outlined so it can be found without being mistaken for owned, and the add form is a panel that
/// opens in place rather than a card.
/// </summary>
public class ShelfSectionsTests : ComponentTestBase
{
    private const string Gin = "11111111-1111-1111-1111-111111111111";
    private const string Vermouth = "22222222-2222-2222-2222-222222222222";

    private const string Catalog = $$"""
        [
          {"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":"London dry gin","isAvailable":true,"isOwn":false},
          {"id":"{{Vermouth}}","name":"Sweet vermouth","category":"Vermouth","subcategory":null,"isAvailable":false,"isOwn":false}
        ]
        """;

    private const string VermouthUnlocks = $$"""
        [{"ingredientId":"{{Vermouth}}","ingredient":"Sweet vermouth","unlocks":4,"cocktails":["Negroni","Manhattan","Martinez","Rob Roy"]}]
        """;

    private const string Categories = """
        [{"id":"44444444-4444-4444-4444-444444444444","name":"Gin","subcategories":[]}]
        """;

    private IRenderedComponent<Shelf> RenderShelf()
    {
        Http.On(HttpMethod.Get, "/api/inventory", Catalog);
        Http.On(HttpMethod.Get, "/api/cocktails", """{"items":[],"page":1,"pageSize":1,"total":3}""");
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", VermouthUnlocks);
        Http.On(HttpMethod.Get, "/api/cocktails/starters", "[]");
        Http.On(HttpMethod.Get, "/api/inventory/categories", Categories);
        return Render<Shelf>();
    }

    [Fact]
    public void CategoriesAreSectionsOnAHairline_NotCards()
    {
        var page = RenderShelf();

        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll(".card"));

            // The anchor and its test id survive on the section; the heading is the serif.
            var gin = page.Find("#cat-gin");
            Assert.Equal("shelf-cat-gin", gin.GetAttribute("data-testid"));
            Assert.NotNull(gin.QuerySelector("h2.font-display"));
            Assert.Equal("Shelf_CategoryCount[1, 1]", page.Find("[data-testid='shelf-cat-count-gin']").TextContent.Trim());
        });
    }

    [Fact]
    public void TheBottleSheNames_IsOutlined_AndNotOwned()
    {
        var page = RenderShelf();

        page.WaitForAssertion(() =>
        {
            Assert.Equal("Marga_ShelfNext[Sweet vermouth, 4]", page.Find("[data-testid='shelf-marga'] .marga-line").TextContent.Trim());

            var vermouth = page.Find($"[data-testid='shelf-item-{Vermouth}']");
            var vermouthPill = page.Find($"label[for='{vermouth.GetAttribute("id")}']");
            Assert.Contains("shelf-pill-named", vermouthPill.ClassList);
            Assert.False(vermouth.HasAttribute("checked"), "the named bottle must never read as owned");

            var gin = page.Find($"[data-testid='shelf-item-{Gin}']");
            var ginPill = page.Find($"label[for='{gin.GetAttribute("id")}']");
            Assert.DoesNotContain("shelf-pill-named", ginPill.ClassList);
        });
    }

    [Fact]
    public void ThePayoffCountIsInTheDisplayFace_AndSheIsTheCardTone()
    {
        var page = RenderShelf();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("font-display", page.Find("[data-testid='shelf-count']").ClassList);
            Assert.NotNull(page.Find("[data-testid='shelf-marga'] .marga-card"));
        });
    }

    [Fact]
    public void TheAddFormOpensInPlace_AsAPanelNotACard()
    {
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find("[data-testid='shelf-add-in-gin']"));

        page.Find("[data-testid='shelf-add-in-gin']").Click();

        page.WaitForAssertion(() =>
        {
            Assert.NotNull(page.Find("#cat-gin .shelf-add-form"));
            Assert.Empty(page.FindAll(".card"));
        });
    }
}
