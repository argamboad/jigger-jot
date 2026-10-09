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
        foreach (var id in new[] { "[data-testid='new-name']", "[data-testid='new-serving']", "[data-testid='new-glass']", "[data-testid='new-method']", "[data-testid='new-instructions']" })
            Assert.NotNull(page.Find($".write-drink {id}"));

        // A line's five controls are all in the row, none behind an open step — the handoff's popover
        // was refused (F4) because it would hide two ids the journeys fill.
        var line = page.Find("[data-testid='new-line']");
        foreach (var id in new[] { "[data-testid='new-line-amount']", "[data-testid='new-line-unit']", "[data-testid='new-line-ingredient']", "[data-testid='new-line-role']", "[data-testid='new-line-required']", "[data-testid='new-line-remove']" })
            Assert.NotNull(line.QuerySelector(id));
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
        page.Find("[data-testid='new-glass']").Change(Coupe);
        // AUTHORING-4: the ingredient is a combobox now — type the name, pick the option.
        page.Find("[data-testid='new-line-ingredient']").Input("London dry gin");
        page.Find("[data-testid='new-line-ingredient-option']").MouseDown();
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

    private const string Coupe = "11111111-1111-1111-1111-111111111111";

    /// <summary>A recipe the form would send but for its glass: a name and one picked ingredient.</summary>
    private static void NameAndALine(IRenderedComponent<WriteCocktail> page)
    {
        page.Find("[data-testid='new-name']").Change("Gamboa Sour");
        page.Find("[data-testid='new-line-ingredient']").Input("London dry gin");
        page.Find("[data-testid='new-line-ingredient-option']").MouseDown();
    }

    [Fact]
    public async Task NoGlass_MargaSaysSoBesideTheGlass_AndNothingIsSent()
    {
        var page = RenderForm();
        NameAndALine(page);

        await page.Find("[data-testid='new-save']").ClickAsync(new());

        // JJ-043: the glass is required, and the form says so in her voice, beside the field it is about —
        // the compact aside, since the page already has its page-level Marga. Nothing goes to the API.
        page.WaitForAssertion(() =>
        {
            var aside = page.Find(".write-drink [data-testid='new-glass-marga']");
            Assert.Contains("Marga_GlassRequired", aside.TextContent);
            Assert.NotNull(aside.QuerySelector("[data-testid='marga']"));
            Assert.Contains("is-invalid", page.Find("[data-testid='new-glass']").ClassList);
        });
        Assert.DoesNotContain(Http.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task PickingAGlass_SendsHerAway()
    {
        var page = RenderForm();
        NameAndALine(page);
        await page.Find("[data-testid='new-save']").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("[data-testid='new-glass-marga']"));

        page.Find("[data-testid='new-glass']").Change(Coupe);

        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll("[data-testid='new-glass-marga']"));
            Assert.DoesNotContain("is-invalid", page.Find("[data-testid='new-glass']").ClassList);
        });
    }

    [Fact]
    public async Task TheServersGlassRequired_IsHerLineToo()
    {
        // A client that skipped the check still gets the same answer, in the same place.
        Http.On(HttpMethod.Post, "/api/cocktails", """{"error":"glass_required","message":"A cocktail needs its glass"}""", HttpStatusCode.BadRequest);
        var page = RenderForm();
        NameAndALine(page);
        page.Find("[data-testid='new-glass']").Change(Coupe);

        await page.Find("[data-testid='new-save']").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Marga_GlassRequired", page.Find("[data-testid='new-glass-marga']").TextContent);
            Assert.Empty(page.FindAll("[data-testid='new-error']"));
        });
    }

    [Fact]
    public void TheGlassOffersNo_NotStated_Option()
    {
        var page = RenderForm();

        // There is no "not stated" glass any more (JJ-043): the first option is a prompt, and choosing a
        // glass is the only way past it.
        var options = page.Find("[data-testid='new-glass']").QuerySelectorAll("option");
        Assert.DoesNotContain(options, o => o.TextContent.Contains("Cocktail_NotStated"));
        Assert.Equal(string.Empty, options[0].GetAttribute("value"));
        Assert.Contains("Cocktail_ChooseGlass", options[0].TextContent);
    }
}
