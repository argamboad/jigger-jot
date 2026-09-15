using Bunit;
using JiggerJot.Shared.Ui.Components;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// MARGA-7: the ‹ 2 of 7 › under every line where Marga names a bottle to buy. One component, so the five
/// places she suggests a bottle page through it the same way.
/// </summary>
public class BottlePagerTests : ComponentTestBase
{
    private IRenderedComponent<BottlePager> RenderPager(int index, int count, Action<int>? changed = null) =>
        Render<BottlePager>(ps => ps
            .Add(p => p.Index, index)
            .Add(p => p.Count, count)
            .Add(p => p.IndexChanged, (int i) => changed?.Invoke(i)));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OneBottleOrNone_HasNothingToPageThrough(int count)
    {
        var cut = RenderPager(0, count);

        Assert.Empty(cut.FindAll("[data-testid='bottle-pager']"));
    }

    [Fact]
    public void ItSaysWhereYouAre_CountingFromOne()
    {
        var cut = RenderPager(1, 7);

        Assert.Equal("Marga_BottlePosition[2, 7]", cut.Find("[data-testid='bottle-position']").TextContent.Trim());
    }

    [Fact]
    public void AtTheBestBottle_ThereIsNothingBefore()
    {
        var cut = RenderPager(0, 4);

        Assert.True(cut.Find("[data-testid='bottle-prev']").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid='bottle-next']").HasAttribute("disabled"));
    }

    [Fact]
    public void AtTheLastBottle_ItStops_RatherThanWrappingAround()
    {
        var cut = RenderPager(3, 4);

        Assert.False(cut.Find("[data-testid='bottle-prev']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='bottle-next']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task TheArrowsMoveOneBottleEachWay()
    {
        var moves = new List<int>();
        var cut = RenderPager(2, 5, moves.Add);

        await cut.Find("[data-testid='bottle-next']").ClickAsync(new());
        await cut.Find("[data-testid='bottle-prev']").ClickAsync(new());

        Assert.Equal([3, 1], moves);
    }

    [Fact]
    public void BothArrowsAreRealButtons_WithNamesAScreenReaderCanSay()
    {
        var cut = RenderPager(1, 3);

        foreach (var (id, label) in new[] { ("bottle-prev", "Marga_BottlePrevious"), ("bottle-next", "Marga_BottleNext") })
        {
            var button = cut.Find($"[data-testid='{id}']");
            Assert.Equal("button", button.TagName.ToLowerInvariant());
            Assert.Equal("button", button.GetAttribute("type"));
            Assert.Equal(label, button.GetAttribute("aria-label"));
        }
    }

    [Fact]
    public void TenIsTheMostItEverOffers()
    {
        Assert.Equal(10, BottlePager.MaxBottles);
    }
}
