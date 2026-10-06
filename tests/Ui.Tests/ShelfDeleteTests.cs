using System.Net;
using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// INV-4 on the shelf: a bottle the household added carries a delete button beside its pill — never
/// inside the label, which is the checkbox's click target — and a bottle one of its recipes still uses
/// is refused with those recipes named.
/// </summary>
public class ShelfDeleteTests : ComponentTestBase
{
    private const string Gin = "11111111-1111-1111-1111-111111111111";
    private const string Syrup = "33333333-3333-3333-3333-333333333333";

    private const string Shelf = $$"""
        [
          {"id":"{{Gin}}","name":"London dry gin","category":"Gin","subcategory":null,"isAvailable":true,"isOwn":false},
          {"id":"{{Syrup}}","name":"Pineapple syrup","category":"Syrups","subcategory":null,"isAvailable":true,"isOwn":true}
        ]
        """;

    private IRenderedComponent<Shelf> RenderShelf()
    {
        Http.On(HttpMethod.Get, "/api/inventory", Shelf);
        Http.On(HttpMethod.Get, "/api/cocktails", """{"items":[],"page":1,"pageSize":1,"total":0}""");
        Http.On(HttpMethod.Get, "/api/cocktails/unlocks", "[]");
        var page = Render<Shelf>();
        page.WaitForAssertion(() => page.Find($"[data-testid='shelf-item-{Syrup}']"));
        return page;
    }

    [Fact]
    public void OnlyMyOwnBottles_CarryADeleteButton_OutsideTheLabel()
    {
        var page = RenderShelf();

        Assert.Empty(page.FindAll($"[data-testid='shelf-delete-{Gin}']"));
        var delete = page.Find($"[data-testid='shelf-delete-{Syrup}']");
        Assert.Equal("button", delete.TagName.ToLowerInvariant());
        Assert.Equal("Shelf_DeleteLabel[Pineapple syrup]", delete.GetAttribute("aria-label"));
        Assert.Null(delete.Closest("label"));
    }

    [Fact]
    public void ConfirmingDelete_TakesTheBottleOffTheShelf()
    {
        Http.On(HttpMethod.Delete, $"/api/inventory/ingredients/{Syrup}", status: HttpStatusCode.NoContent);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var page = RenderShelf();

        page.Find($"[data-testid='shelf-delete-{Syrup}']").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll($"[data-testid='shelf-item-{Syrup}']")));
        Assert.Contains("Shelf_DeleteConfirm[Pineapple syrup]",
            JSInterop.Invocations["confirm"].Single().Arguments.Single()!.ToString());
        page.Find($"[data-testid='shelf-item-{Gin}']");
    }

    [Fact]
    public void CancellingTheConfirmation_DeletesNothing()
    {
        var page = RenderShelf();

        page.Find($"[data-testid='shelf-delete-{Syrup}']").Click();

        Assert.DoesNotContain(Http.Requests, r => r.Method == HttpMethod.Delete);
        page.Find($"[data-testid='shelf-item-{Syrup}']");
    }

    [Theory]
    [InlineData("""["Tiki Sour"]""", "Shelf_DeleteInUseOne[Pineapple syrup, Tiki Sour]")]
    [InlineData("""["Painkiller","Tiki Sour","Zombie"]""", "Shelf_DeleteInUseMany[Pineapple syrup, Painkiller, 2]")]
    public void ABottleARecipeUses_IsRefused_AndTheRecipesAreNamed(string usedIn, string expected)
    {
        Http.On(HttpMethod.Delete, $"/api/inventory/ingredients/{Syrup}",
            $$"""{"error":"ingredient_in_use","message":"In use","usedIn":{{usedIn}}}""", HttpStatusCode.Conflict);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var page = RenderShelf();

        page.Find($"[data-testid='shelf-delete-{Syrup}']").Click();

        page.WaitForAssertion(() =>
            Assert.Equal(expected, page.Find("[data-testid='shelf-delete-error']").TextContent.Trim()));
        page.Find($"[data-testid='shelf-item-{Syrup}']");
    }
}
