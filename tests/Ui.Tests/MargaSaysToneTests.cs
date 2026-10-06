using Bunit;
using JiggerJot.Shared.Ui.Components;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-1: <c>MargaSays</c> gains a <c>Tone</c>. Card is her at 96px with a brass label ABOVE the
/// line and the line in the display face; Inline is her at 32px, sans, no label. The old
/// <c>Size</c> and <c>Compact</c> parameters keep working unchanged, so every call site compiles and
/// improves in its own slice rather than all at once.
/// </summary>
public class MargaSaysToneTests : ComponentTestBase
{
    [Fact]
    public void CardTone_IsNinetySix_WithTheLabelBeforeTheLine()
    {
        var cut = Render<MargaSays>(ps => ps
            .Add(p => p.Tone, MargaTone.Card)
            .Add(p => p.Aside, "Marga · checked your shelf a moment ago")
            .AddChildContent("Pick up Sweet vermouth and I can make you 6 more."));

        var face = cut.Find("img.marga-face");
        Assert.Equal("96", face.GetAttribute("width"));
        Assert.Equal("96", face.GetAttribute("height"));

        // The aside becomes a LABEL and moves above the sentence — the handoff's "MARGA · CHECKED YOUR
        // SHELF A MOMENT AGO" in brass, then her line in the serif. Same string, new position.
        // One query, one snapshot: bUnit hands back a fresh DOM per Find, so two elements from two
        // calls never share a parent and order has to be read off a single tree.
        var body = cut.Find(".marga-body").Children;
        Assert.Contains("marga-label", body[0].ClassList);
        Assert.Contains("marga-line", body[1].ClassList);
        Assert.Equal("Marga · checked your shelf a moment ago", body[0].TextContent.Trim());
        Assert.Contains("font-display", body[1].ClassList);
        Assert.Empty(cut.FindAll(".marga-aside"));
    }

    [Fact]
    public void InlineTone_IsThirtyTwo_SansAndSilentAboutTheLabel()
    {
        var cut = Render<MargaSays>(ps => ps
            .Add(p => p.Tone, MargaTone.Inline)
            .Add(p => p.Aside, "should not render")
            .AddChildContent("No Cointreau? Triple sec's fine."));

        var face = cut.Find("img.marga-face");
        Assert.Equal("32", face.GetAttribute("width"));
        Assert.Empty(cut.FindAll(".marga-label"));
        Assert.Empty(cut.FindAll(".marga-aside"));
        Assert.DoesNotContain("font-display", cut.Find(".marga-line").ClassList);
    }

    [Fact]
    public void ASizeWithoutATone_RendersExactlyAsBefore()
    {
        var cut = Render<MargaSays>(ps => ps
            .Add(p => p.Size, 40)
            .Add(p => p.Aside, "who said it")
            .AddChildContent("Tell me what's on your shelf."));

        var face = cut.Find("img.marga-face");
        Assert.Equal("40", face.GetAttribute("width"));

        // The pre-Tone shape: the aside sits UNDER the line, small and quiet; no label, no serif.
        var line = cut.Find(".marga-line");
        var aside = line.QuerySelector(".marga-aside");
        Assert.NotNull(aside);
        Assert.Contains("marga-aside", line.Children.Last().ClassList);
        Assert.Empty(cut.FindAll(".marga-label"));
        Assert.DoesNotContain("font-display", line.ClassList);
    }

    [Fact]
    public void SheStaysDecorative_InEveryTone()
    {
        foreach (var tone in new[] { MargaTone.Card, MargaTone.Inline })
        {
            var cut = Render<MargaSays>(ps => ps.Add(p => p.Tone, tone).AddChildContent("x"));
            var face = cut.Find("img.marga-face");
            Assert.Equal("", face.GetAttribute("alt"));
            Assert.Equal("true", face.GetAttribute("aria-hidden"));
        }
    }
}
