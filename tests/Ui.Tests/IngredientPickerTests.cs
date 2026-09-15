using Bunit;
using Microsoft.AspNetCore.Components.Web;
using JiggerJot.Shared.Ui.Components;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// The searchable ingredient picker on the write form. A plain select of two hundred bottles was a
/// scroll, not a picker; this is a combobox — type to narrow by name or category, pick with the
/// mouse or the keyboard — that screen readers can still announce as one.
/// </summary>
public class IngredientPickerTests : ComponentTestBase
{
    private static readonly Guid Gin = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Lime = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Lemon = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SlowGin = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly IngredientOption[] Options =
    [
        new(Gin, "London dry gin", "Gin", OnShelf: true),
        new(Lime, "Lime juice", "Juice", OnShelf: false),
        new(Lemon, "Lemon juice", "Juice", OnShelf: true),
        new(SlowGin, "Sloe gin", "Gin", OnShelf: false),
    ];

    private IRenderedComponent<IngredientPicker> RenderPicker(Guid? value = null, Action<Guid?>? changed = null) =>
        Render<IngredientPicker>(ps => ps
            .Add(p => p.Options, Options)
            .Add(p => p.Value, value)
            .Add(p => p.ValueChanged, (Guid? v) => changed?.Invoke(v))
            .Add(p => p.TestId, "new-line-ingredient"));

    private static List<string> Shown(IRenderedComponent<IngredientPicker> cut) =>
        [.. cut.FindAll("[data-testid='new-line-ingredient-option'] .ing-picker-name").Select(e => e.TextContent.Trim())];

    [Fact]
    public void TypingNarrowsTheList_AndNamesThatStartWithItComeFirst()
    {
        var cut = RenderPicker();

        cut.Find("[data-testid='new-line-ingredient']").Input("gin");

        // "London dry gin" contains it; "Sloe gin" contains it too; nothing starts with it, so both
        // stay, alphabetical. Juices are gone.
        Assert.Equal(["London dry gin", "Sloe gin"], Shown(cut));

        cut.Find("[data-testid='new-line-ingredient']").Input("l");
        Assert.Equal(["Lemon juice", "Lime juice", "London dry gin", "Sloe gin"], Shown(cut));
    }

    [Fact]
    public void TheCategoryMatchesToo_SoJuiceFindsEveryJuice()
    {
        var cut = RenderPicker();

        cut.Find("[data-testid='new-line-ingredient']").Input("juice");

        Assert.Equal(["Lemon juice", "Lime juice"], Shown(cut));
    }

    [Fact]
    public void PickingWithTheMouse_SetsTheValue_AndTheBoxShowsTheName()
    {
        Guid? picked = null;
        var cut = RenderPicker(changed: v => picked = v);

        cut.Find("[data-testid='new-line-ingredient']").Input("lime");
        // Mousedown rather than click: a click lands after the input's blur, which closes the list.
        cut.Find("[data-testid='new-line-ingredient-option']").MouseDown();

        Assert.Equal(Lime, picked);
        Assert.Empty(cut.FindAll("[data-testid='new-line-ingredient-option']"));
    }

    [Fact]
    public void TheBoxShowsTheChosenBottlesName()
    {
        var cut = RenderPicker(value: Lemon);

        Assert.Equal("Lemon juice", cut.Find("[data-testid='new-line-ingredient']").GetAttribute("value"));
    }

    [Fact]
    public void TheKeyboardPicksToo()
    {
        Guid? picked = null;
        var cut = RenderPicker(changed: v => picked = v);
        var box = cut.Find("[data-testid='new-line-ingredient']");

        box.Input("gin");
        box.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        box.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        box.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(SlowGin, picked);
    }

    [Fact]
    public void EscapeClosesTheList_WithoutChangingAnything()
    {
        Guid? picked = null;
        var cut = RenderPicker(value: Gin, changed: v => picked = v);
        var box = cut.Find("[data-testid='new-line-ingredient']");

        box.Input("lime");
        box.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Null(picked);
        Assert.Empty(cut.FindAll("[data-testid='new-line-ingredient-option']"));
        Assert.Equal("London dry gin", cut.Find("[data-testid='new-line-ingredient']").GetAttribute("value"));
    }

    [Fact]
    public void NothingMatching_SaysSo_RatherThanShowingAnEmptyList()
    {
        var cut = RenderPicker();

        cut.Find("[data-testid='new-line-ingredient']").Input("yuzu");

        Assert.Empty(cut.FindAll("[data-testid='new-line-ingredient-option']"));
        Assert.Contains("Cocktail_NoIngredientMatch", cut.Find("[data-testid='new-line-ingredient-empty']").TextContent);
    }

    [Fact]
    public void BottlesOnTheShelf_AreMarked()
    {
        var cut = RenderPicker();

        cut.Find("[data-testid='new-line-ingredient']").Input("juice");

        var options = cut.FindAll("[data-testid='new-line-ingredient-option']");
        Assert.Contains("Cocktail_OnShelf", options[0].TextContent);      // Lemon juice, ticked
        Assert.DoesNotContain("Cocktail_OnShelf", options[1].TextContent); // Lime juice, not
    }

    [Fact]
    public void ItIsAComboboxAScreenReaderCanUse()
    {
        var cut = RenderPicker();
        var box = cut.Find("[data-testid='new-line-ingredient']");

        Assert.Equal("combobox", box.GetAttribute("role"));
        Assert.Equal("list", box.GetAttribute("aria-autocomplete"));
        Assert.Equal("false", box.GetAttribute("aria-expanded"));

        box.Input("gin");
        box.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        box = cut.Find("[data-testid='new-line-ingredient']");
        Assert.Equal("true", box.GetAttribute("aria-expanded"));
        var list = cut.Find("[role='listbox']");
        Assert.Equal(list.Id, box.GetAttribute("aria-controls"));

        var active = cut.Find("[role='option'][aria-selected='true']");
        Assert.Equal(active.Id, box.GetAttribute("aria-activedescendant"));
    }
}
