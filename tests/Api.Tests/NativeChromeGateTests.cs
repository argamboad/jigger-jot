using System.Text.RegularExpressions;

namespace JiggerJot.Api.Tests;

/// <summary>
/// The app as it looks on a phone (2026-09-16), found by running the real Android app on an emulator
/// rather than a 390px browser window. Each rule here is a defect that sweep showed.
/// </summary>
public class NativeChromeGateTests
{
    private static string Read(params string[] parts) =>
        Regex.Replace(File.ReadAllText(Path.Combine([RepoRoot(), .. parts])), @"/\*.*?\*/", "", RegexOptions.Singleline);

    [Fact]
    public void TheTabBar_IsExactlyAsTallAsTheRoomReservedForIt()
    {
        var css = Read("src", "Shared.Ui", "wwwroot", "css", "app.css");

        // Every bar fixed above the tab bar (the shelf's payoff, the recipe's actions, Write's Save) sits
        // at --tab-bar-clearance, which is --tab-bar-height plus the gesture inset. The tab bar itself had
        // no height and came out ~42px against 56px reserved, so each of those bars floated 14px above it
        // with the page showing through the gap.
        var bar = Regex.Match(css, @"\.app-header \.app-tabs\s*\{([^}]*position:\s*fixed[^}]*)\}");
        Assert.True(bar.Success, "no fixed tab-bar rule in app.css");
        Assert.Matches(
            new Regex(@"(?<![-\w])height:\s*calc\(var\(--tab-bar-height[^;]*env\(safe-area-inset-bottom"),
            bar.Groups[1].Value);
    }

    [Fact]
    public void TheAndroidShell_CarriesNoTemplateColour()
    {
        // The platform template's sage (#6B8A72 / #465D4D) painted the status bar above every screen of a
        // copper-and-night app. The bars now follow the app's own surface token in each theme.
        foreach (var file in new[]
                 {
                     Path.Combine("src", "Maui", "Platforms", "Android", "MainActivity.cs"),
                     Path.Combine("src", "Maui", "Platforms", "Android", "Resources", "values", "colors.xml"),
                 })
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot(), file));
            Assert.DoesNotContain("6B8A72", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("465D4D", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheAndroidShell_PadsForTheStatusBarOnce()
    {
        var activity = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Maui", "Platforms", "Android", "MainActivity.cs"));

        // The shell pads its content below the status bar. Current Android WebViews also report that inset
        // to CSS, so the header's env(safe-area-inset-top) added it a second time — a 55px empty band.
        // The shell hands the WebView insets with the top already spent.
        Assert.Contains("SetPadding(bars.Left, bars.Top, bars.Right, 0)", activity);
        Assert.Matches(new Regex(@"SetInsets\(\s*WindowInsetsCompat\.Type\.StatusBars\(\)"), activity);
    }

    [Fact]
    public void ThemeJs_TellsAWatcherEveryThemeItApplies()
    {
        var js = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Shared.Ui", "wwwroot", "js", "theme.js"));

        Assert.Contains("watch:", js);
        Assert.Contains("invokeMethodAsync('OnThemeApplied'", js);
    }

    [Fact]
    public void Rebranding_NamesTheAndroidSystemBars()
    {
        var rebranding = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "REBRANDING.md"));

        Assert.Contains("Platforms/Android/Resources/values/colors.xml", rebranding);
        Assert.Contains("SystemBarColors", rebranding);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root.");
    }
}
