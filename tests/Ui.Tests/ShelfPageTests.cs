using System.Net;
using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// INV-1's shelf screen. The API half has its own tests; these are about the three things that live
/// only in the component and that a server test cannot see: the running count, the optimistic tick,
/// and the rollback when the save fails.
/// </summary>
public class ShelfPageTests : ComponentTestBase
{
    private const string Gin = "11111111-1111-1111-1111-111111111111";
    private const string Rum = "22222222-2222-2222-2222-222222222222";

    private const string Shelf = $$"""
        [
          {"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":"London dry gin","isAvailable":false,"isOwn":false},
          {"id":"{{Rum}}","name":"White rum","category":"Rum","subcategory":"White rum","isAvailable":false,"isOwn":false}
        ]
        """;

    private IRenderedComponent<Shelf> RenderShelf()
    {
        Http.On(HttpMethod.Get, "/api/inventory", Shelf);
        return Render<Shelf>();
    }

    [Fact]
    public void Shelf_ShowsTheCatalogGroupedByCategory()
    {
        var page = RenderShelf();

        page.WaitForAssertion(() =>
            Assert.Equal(2, page.FindAll("[data-testid^='shelf-item-']").Count));

        // Grouped, because 191 of these in a flat list is not a screen anyone fills in.
        var markup = page.Markup;
        Assert.Contains("Gin", markup);
        Assert.Contains("Rum", markup);
    }

    [Fact]
    public void Ticking_UpdatesTheCountImmediately()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.NoContent);
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(true);

        // The count is what tells a household how far it has got, so it has to move with the tick
        // rather than after a round trip.
        page.WaitForAssertion(() =>
            Assert.Contains("1", page.Find("[data-testid='shelf-count']").TextContent));
    }

    [Fact]
    public void Unticking_PutsTheCountBackDown()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.NoContent);
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(true);
        page.WaitForAssertion(() =>
            Assert.Contains("1", page.Find("[data-testid='shelf-count']").TextContent));

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(false);
        page.WaitForAssertion(() =>
            Assert.Contains("0", page.Find("[data-testid='shelf-count']").TextContent));
    }

    [Fact]
    public void AFailedSave_RollsTheTickBack_AndSaysSo()
    {
        Http.On(HttpMethod.Put, $"/api/inventory/{Gin}", status: HttpStatusCode.InternalServerError);
        var page = RenderShelf();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Gin}']"));

        page.Find($"[data-testid='shelf-item-{Gin}']").Change(true);

        // Optimism is only honest if it is undone when the save fails. A tick that stays put after a
        // failed write is a lie the household finds out about later, when a drink they were promised
        // turns out not to be makeable.
        page.WaitForAssertion(() => page.Find("[data-testid='shelf-error']"));
        Assert.Contains("0", page.Find("[data-testid='shelf-count']").TextContent);
    }

    [Fact]
    public void AFailedLoad_SaysSo_RatherThanShowingAnEmptyShelf()
    {
        Http.On(HttpMethod.Get, "/api/inventory", "{}", HttpStatusCode.InternalServerError);

        var page = Render<Shelf>();

        // An empty shelf and a broken shelf look identical, and only one of them is the household's
        // fault to fix.
        page.WaitForAssertion(() => page.Find("[data-testid='shelf-error']"));
    }
}
