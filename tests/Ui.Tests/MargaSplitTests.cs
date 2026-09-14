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
    public void TheAsideIsOptional()
    {
        var cut = Render<MargaSplit>(ps => ps
            .Add(p => p.Line, "Authentication failed")
            .AddChildContent("x"));

        Assert.Empty(cut.FindAll(".split-eyebrow"));
    }
}
