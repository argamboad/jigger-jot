using System.Text.RegularExpressions;

namespace JiggerJot.Api.Tests.App;

/// <summary>
/// JiggerJot's own gates on its boot screen (SHELL-2). They lived in the platform's <c>EnforcementGateTests</c>, which
/// stays identical across repos since Arch A1/A2; an app's gates live beside its allowlists.
/// </summary>
public sealed class ShellBootGateTests
{
    [Fact]
    public void HostIndexHtml_RenderTheSameBootState() // SHELL-2, NATIVE_PARITY "index.html sync"
    {
        // The rule was already being broken before there was a boot state to break it with: the web
        // host had the stock two-circle spinner and the MAUI host had the literal word "Loading...".
        // A boot screen that only ships on web is a bug on four native shells, so it is a gate now
        // rather than a line in a document.
        var root = RepoRoot();

        // Comments stripped first: both files explain the parity rule in prose, and a gate that
        // matched its own documentation would pass on a file whose markup had been deleted.
        static string Markup(string path) =>
            Regex.Replace(File.ReadAllText(path), "<!--.*?-->", "", RegexOptions.Singleline);

        var web = Markup(Path.Combine(root, "src", "Web", "wwwroot", "index.html"));
        var maui = Markup(Path.Combine(root, "src", "Maui", "wwwroot", "index.html"));

        foreach (var marker in new[] { "class=\"boot\"", "boot-scene", "class=\"loading-progress\"" })
        {
            Assert.Contains(marker, web, StringComparison.Ordinal);
            Assert.Contains(marker, maui, StringComparison.Ordinal);
        }

        // ...and on the same drawing. Two hosts pointing at two different files would pass every
        // marker above while looking like different apps.
        static string Scene(string html) =>
            Regex.Match(html, @"_content/[A-Za-z0-9./_-]+marga_scene[A-Za-z0-9._-]*\.png").Value;
        Assert.NotEmpty(Scene(web));
        Assert.Equal(Scene(web), Scene(maui));

        // The ONE intended difference, asserted in both directions so it stays deliberate: a WebView
        // loads out of the app package, so there is no download to measure and the arc sweeps rather
        // than reading a --blazor-load-percentage nothing sets.
        Assert.Contains("boot-indeterminate", maui, StringComparison.Ordinal);
        Assert.DoesNotContain("boot-indeterminate", web, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBootMeter_SitsOnTheDrawing() // SHELL-2 amendment, 2026-09-14
    {
        // The maintainer's call on sight: the arc under the drawing read as two things waiting; the
        // arc ON the drawing, centred, reads as one. Both hosts' index.html carry the markup under
        // the parity gate above, so the arrangement is the stylesheet's alone: the boot block is the
        // containing block, the meter is taken out of the flow and centred on it, and the figure
        // inside the ring is painted for the drawing it now sits on rather than for the page ground.
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Shared.Ui", "wwwroot", "css", "app.css"));
        static string Rule(string css, string selector) =>
            Regex.Match(css, @"(?<![\w-])" + Regex.Escape(selector) + @"\s*\{([^}]*)\}").Groups[1].Value;

        Assert.Matches(new Regex(@"position:\s*relative"), Rule(css, ".boot"));
        var meter = Rule(css, ".boot-meter");
        Assert.Matches(new Regex(@"position:\s*absolute"), meter);
        Assert.Matches(new Regex(@"translate\(\s*-50%\s*,\s*-50%\s*\)"), meter);
        Assert.Matches(new Regex(@"border-radius:\s*50%"), meter);
    }

    [Fact]
    public void TheBootIllustration_IsTheOptimisedOne() // SHELL-2
    {
        // The boot screen is the one place this drawing is fetched BEFORE the app is usable, so an
        // unoptimized asset here makes the very wait it decorates longer. It was 2 MB as delivered
        // and ships at ~115 KB; the ceiling leaves room to redraw it without leaving room to paste
        // the original back.
        var scene = new FileInfo(Path.Combine(
            RepoRoot(), "src", "Shared.Ui", "wwwroot", "brand", "marga_scene_512.png"));

        Assert.True(scene.Exists, $"the boot illustration is missing: {scene.FullName}");
        Assert.True(scene.Length < 250 * 1024,
            $"the boot illustration is {scene.Length / 1024} KB — optimize it before it lands in the boot path");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Api", "Features")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root from the test assembly.");
    }
}
