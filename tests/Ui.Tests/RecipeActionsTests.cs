using System.Net;
using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// The recipe's actions under its ingredients (AUTHORING-5): one row, the primary first — Create my own
/// version, then Edit, then Delete as a text link — with Edit and Delete only on a household's own recipe,
/// and Delete behind a confirmation.
/// </summary>
public class RecipeActionsTests : ComponentTestBase
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static string Recipe(bool own) => $$"""
        {"id":"{{Id}}","name":"House Sour","glass":null,"method":null,"servingType":"FullDrink",
         "instructions":null,"source":null,"isOwn":{{own.ToString().ToLowerInvariant()}},"makeability":"NotMakeable",
         "forkedFrom":null,
         "lines":[{"ingredient":"London dry gin","amount":2,"unit":"oz","display":"2 oz","isRequired":true,"role":"Base","notes":null,"availability":"Missing","substituteWith":null}]}
        """;

    private IRenderedComponent<CocktailDetail> RenderRecipe(bool own = true)
    {
        Http.On(HttpMethod.Get, $"/api/cocktails/{Id}", Recipe(own));
        return Render<CocktailDetail>(ps => ps.Add(p => p.Id, Id));
    }

    [Fact]
    public void MyRecipe_ForkFirst_ThenEdit_ThenDeleteAsALink()
    {
        var page = RenderRecipe();

        page.WaitForAssertion(() =>
        {
            var actions = page.Find(".recipe-actions");
            var ids = actions.QuerySelectorAll("[data-testid]").Select(e => e.GetAttribute("data-testid")).ToList();
            Assert.Equal(["cocktail-fork", "cocktail-edit", "cocktail-delete"], ids);

            // The primary leads; Delete reads as a destructive link, not a third button of equal weight.
            Assert.Contains("btn-primary", page.Find("[data-testid='cocktail-fork']").ClassList);
            var delete = page.Find("[data-testid='cocktail-delete']");
            Assert.Contains("btn-link", delete.ClassList);
            Assert.Contains("text-danger", delete.ClassList);
        });
    }

    [Fact]
    public void ABooksRecipe_OffersOnlyTheFork()
    {
        var page = RenderRecipe(own: false);

        page.WaitForAssertion(() =>
        {
            page.Find("[data-testid='cocktail-fork']");
            Assert.Empty(page.FindAll("[data-testid='cocktail-edit']"));
            Assert.Empty(page.FindAll("[data-testid='cocktail-delete']"));
        });
    }

    [Fact]
    public void ConfirmingDelete_RemovesIt_AndGoesBackToTheCatalog()
    {
        Http.On(HttpMethod.Delete, $"/api/cocktails/{Id}", status: HttpStatusCode.NoContent);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var page = RenderRecipe();
        page.WaitForAssertion(() => page.Find("[data-testid='cocktail-delete']"));

        page.Find("[data-testid='cocktail-delete']").Click();

        page.WaitForAssertion(() =>
        {
            Assert.Contains(Http.Requests, r => r.Method == HttpMethod.Delete && r.RequestUri!.AbsolutePath == $"/api/cocktails/{Id}");
            Assert.EndsWith("/cocktails", Services.GetRequiredService<NavigationManager>().Uri);
        });
        Assert.Contains("Cocktail_DeleteConfirm[House Sour]",
            JSInterop.Invocations["confirm"].Single().Arguments.Single()!.ToString());
    }

    [Fact]
    public void CancellingTheConfirmation_DeletesNothing()
    {
        // Loose JSInterop answers an unset confirm with false — the same as pressing Cancel.
        var page = RenderRecipe();
        page.WaitForAssertion(() => page.Find("[data-testid='cocktail-delete']"));

        page.Find("[data-testid='cocktail-delete']").Click();

        Assert.DoesNotContain(Http.Requests, r => r.Method == HttpMethod.Delete);
        Assert.EndsWith($"/", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void AFailedDelete_SaysSo_AndStaysOnTheRecipe()
    {
        Http.On(HttpMethod.Delete, $"/api/cocktails/{Id}", status: HttpStatusCode.InternalServerError);
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var page = RenderRecipe();
        page.WaitForAssertion(() => page.Find("[data-testid='cocktail-delete']"));

        page.Find("[data-testid='cocktail-delete']").Click();

        page.WaitForAssertion(() =>
            Assert.Equal("Cocktail_DeleteFailed", page.Find("[data-testid='cocktail-delete-error']").TextContent.Trim()));
        page.Find("[data-testid='cocktail-name']");
    }
}
