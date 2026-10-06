using Bunit;
using JiggerJot.Shared.Ui.Components;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-5: the split — her scene as half the screen with a line on it, the working side beside.
/// One component so Login, Welcome, Join and the auth error page do not each re-spell it.
/// </summary>
public class MargaSplitTests : ComponentTestBase
{
    [Fact]
    public void HerSceneCarriesTheLine_AndTheWorkIsBeside()
    {
        var cut = Render<MargaSplit>(ps => ps
            .Add(p => p.Line, "Tell me what's on your shelf.")
            .Add(p => p.Aside, "Marga · behind the bar")
            .AddChildContent("<button id=\"work\">Sign in</button>"));

        var scene = cut.Find(".split-scene");
        Assert.NotNull(scene.QuerySelector("img.split-scene-art"));
        Assert.Equal("", scene.QuerySelector("img.split-scene-art")!.GetAttribute("alt"));
        Assert.Equal("Marga · behind the bar", scene.QuerySelector(".split-eyebrow")!.TextContent.Trim());

        // Her scene carries her and her line, nothing else. The wordmark sat in its top corner —
        // over her face at every width — and the working side already carries it; the maintainer
        // took it off on sight. Only one <img> on the scene, and it is her.
        Assert.Single(scene.QuerySelectorAll("img"));

        var line = scene.QuerySelector(".split-line")!;
        Assert.Equal("Tell me what's on your shelf.", line.TextContent.Trim());
        Assert.Contains("font-display", line.ClassList);

        Assert.NotNull(cut.Find(".split-panel #work"));
    }

    [Fact]
    public void FullScreenIsOptIn_SoTheWizardKeepsItsFrame()
    {
        // Login fills the viewport like the sibling apps' sign-in; the wizard sits under the app
        // header and Join and the auth error page have too little beside her to stretch, so the
        // frame is the default and the full bleed is asked for.
        var framed = Render<MargaSplit>(ps => ps.Add(p => p.Line, "x").AddChildContent("x"));
        Assert.DoesNotContain("split-full", framed.Find(".split").ClassList);

        var full = Render<MargaSplit>(ps => ps.Add(p => p.Line, "x").Add(p => p.Full, true).AddChildContent("x"));
        Assert.Contains("split-full", full.Find(".split").ClassList);
    }

    [Fact]
    public void HerSceneIsOneStickyBlock_SoALongPanelCannotStretchHer()
    {
        // The wizard's second step is 191 pills tall, and her column is the grid row's height — so the
        // 512 square was covering a 5000px cell and the page scrolled past a giant crop of her face.
        // The art, the gradient and her line live in ONE positioned block that sticks at viewport
        // height inside the column; the column keeps the night ground beneath it.
        var framed = Render<MargaSplit>(ps => ps.Add(p => p.Line, "x").Add(p => p.Aside, "y").AddChildContent("x"));
        var stick = framed.Find(".split-scene > .split-scene-stick");
        Assert.NotNull(stick.QuerySelector("img.split-scene-art"));
        Assert.NotNull(stick.QuerySelector(".split-scene-text .split-line"));
        Assert.Single(framed.FindAll(".split-scene-stick"));

        // Full bleed too, where the gradient is an element: it rides in the same block.
        var full = Render<MargaSplit>(ps => ps.Add(p => p.Line, "x").Add(p => p.Full, true).AddChildContent("x"));
        var fullStick = full.Find(".split-scene > .split-scene-stick");
        Assert.NotNull(fullStick.QuerySelector("img.split-scene-art"));
        Assert.NotNull(fullStick.QuerySelector(".split-scene-shade"));
        Assert.NotNull(fullStick.QuerySelector(".split-scene-text"));
    }

    [Fact]
    public void WholeIsOptIn_SoTheSceneKeepsHerSquare()
    {
        // Login asks for the whole drawing: the scene column keeps the square's aspect at every width
        // rather than covering a column of whatever shape the panel makes, and on a phone the panel
        // sits under the scene instead of sliding up over her.
        var plain = Render<MargaSplit>(ps => ps.Add(p => p.Line, "x").AddChildContent("x"));
        Assert.DoesNotContain("split-whole", plain.Find(".split").ClassList);

        var whole = Render<MargaSplit>(ps => ps.Add(p => p.Line, "x").Add(p => p.Whole, true).AddChildContent("x"));
        Assert.Contains("split-whole", whole.Find(".split").ClassList);
    }

    [Fact]
    public void TheAsideIsOptional()
    {
        var cut = Render<MargaSplit>(ps => ps
            .Add(p => p.Line, "Authentication failed")
            .AddChildContent("x"));

        Assert.Empty(cut.FindAll(".split-eyebrow"));
    }
}
