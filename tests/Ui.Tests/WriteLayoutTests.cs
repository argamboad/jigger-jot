using System.Net;
using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-6, the write page (handoff page 15): two columns, not one long form; the amount previews in
/// the display face; every field of a line stays visible without opening anything (JJ-036 — no
/// popover); errors attach to the field, and <c>new-error</c> keeps its place above Save for server
/// failures.
/// </summary>
public class WriteLayoutTests : ComponentTestBase
{
    private const string Lookups = """
        {"glasses":[{"id":"11111111-1111-1111-1111-111111111111","name":"Coupe"}],
         "methods":[{"id":"22222222-2222-2222-2222-222222222222","name":"Shake"}],
         "units":[{"id":"33333333-3333-3333-3333-333333333333","name":"ml"}],
         "servingTypes":["FullDrink","Shot"],
         "roles":["Base","Modifier","Garnish"]}
        """;

    private const string Inventory = """
        [{"id":"44444444-4444-4444-4444-444444444444","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":true,"isOwn":false}]
        """;

    private IRenderedComponent<WriteCocktail> RenderForm()
    {
        Http.On(HttpMethod.Get, "/api/cocktails/lookups", Lookups);
        Http.On(HttpMethod.Get, "/api/inventory", Inventory);
        var page = Render<WriteCocktail>();
        page.WaitForAssertion(() => page.Find("[data-testid='new-name']"));
        return page;
    }

    [Fact]
    public void TwoColumns_NoCards_AndEveryFieldOfALineIsVisible()
    {
        var page = RenderForm();

        Assert.Empty(page.FindAll(".card"));
        Assert.NotNull(page.Find(".write-grid"));
        Assert.NotNull(page.Find("h1.page-title"));

        // The drink on the left, its ingredients on the right; every id of the drink survives.
        foreach (var id in new[] { "new-name", "new-serving", "new-glass", "new-method", "new-instructions" })
            Assert.NotNull(page.Find($".write-drink [data-testid='{id}']"));

        // A line's five controls are all in the row, none behind an open step — the handoff's popover
        // was refused (F4) because it would hide two ids the journeys fill.
        var line = page.Find("[data-testid='new-line']");
        foreach (var id in new[] { "new-line-amount", "new-line-unit", "new-line-ingredient", "new-line-role", "new-line-required", "new-line-remove" })
            Assert.NotNull(line.QuerySelector($"[data-testid='{id}']"));
        Assert.Empty(line.QuerySelectorAll("[hidden], .popover, details"));
    }

    [Fact]
    public void TheAmountPreviewsInTheDisplayFace()
    {
        var page = RenderForm();
        Assert.Contains("font-display", page.Find("[data-testid='new-line-amount']").ClassList);
    }

    [Fact]
    public async Task ClientValidationAttachesToTheField_NotASummaryAlert()
    {
        var page = RenderForm();

        // Nothing filled in: the name field is marked, and there is no alert at the top.
        await page.Find("[data-testid='new-save']").ClickAsync(new());
        page.WaitForAssertion(() =>
        {
            Assert.Contains("is-invalid", page.Find("[data-testid='new-name']").ClassList);
            Assert.Empty(page.FindAll("[data-testid='new-error']"));
        });

        // A name but no complete line: the message sits with the lines.
        page.Find("[data-testid='new-name']").Change("Gamboa Sour");
        await page.Find("[data-testid='new-save']").ClickAsync(new());
        page.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("is-invalid", page.Find("[data-testid='new-name']").ClassList);
            Assert.NotNull(page.Find("[data-testid='new-lines'] ~ [data-testid='new-lines-error'], [data-testid='new-lines-error']"));
            Assert.Empty(page.FindAll("[data-testid='new-error']"));
        });
    }

    [Fact]
    public async Task AServerFailureKeepsNewErrorAboveSave()
    {
        Http.On(HttpMethod.Post, "/api/cocktails", """{"error":"invalid_line","message":"bad"}""", HttpStatusCode.BadRequest);
        var page = RenderForm();

        page.Find("[data-testid='new-name']").Change("Gamboa Sour");
        page.Find("[data-testid='new-line-ingredient']").Change("44444444-4444-4444-4444-444444444444");
        await page.Find("[data-testid='new-save']").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            var error = page.Find("[data-testid='new-error']");
            var save = page.Find("[data-testid='new-save']");
            var siblings = error.ParentElement!.Children.ToList();
            Assert.True(siblings.IndexOf(error) < siblings.IndexOf(siblings.First(e => e.GetAttribute("data-testid") == "new-save")),
                "new-error keeps its place above Save");
            Assert.Contains("Cocktail_ErrLine", error.TextContent);
        });
    }
}
