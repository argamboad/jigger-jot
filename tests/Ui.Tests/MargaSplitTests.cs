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
            .Add(p => p.ShowLockup, true)
            .AddChildContent("<button id=\"work\">Sign in</button>"));

        var scene = cut.Find(".split-scene");
        Assert.NotNull(scene.QuerySelector("img.split-scene-art"));
        Assert.Equal("", scene.QuerySelector("img.split-scene-art")!.GetAttribute("alt"));
        Assert.NotNull(scene.QuerySelector("img.split-scene-lockup"));
        Assert.Equal("Marga · behind the bar", scene.QuerySelector(".split-eyebrow")!.TextContent.Trim());

        var line = scene.QuerySelector(".split-line")!;
        Assert.Equal("Tell me what's on your shelf.", line.TextContent.Trim());
        Assert.Contains("font-display", line.ClassList);

        Assert.NotNull(cut.Find(".split-panel #work"));
    }

    [Fact]
    public void TheLockupAndTheAsideAreOptional()
    {
        var cut = Render<MargaSplit>(ps => ps
            .Add(p => p.Line, "Authentication failed")
            .AddChildContent("x"));

        Assert.Empty(cut.FindAll(".split-scene-lockup"));
        Assert.Empty(cut.FindAll(".split-eyebrow"));
    }
}
