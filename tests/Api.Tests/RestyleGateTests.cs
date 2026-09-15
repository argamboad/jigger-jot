using System.Text.RegularExpressions;
using Xunit;

namespace JiggerJot.Api.Tests;

/// <summary>
/// BACKBAR-1 (JJ-038): the display serif is self-hosted, licensed, and never synthesised bold.
/// <para>
/// Repo gates, beside the index.html parity gates, because the two things they hold are the two a
/// reviewer cannot see in a diff: a stylesheet that quietly reaches for a font host (one request to a
/// third party on the login route, before anyone has consented to anything), and a display element
/// that also asks for bold — the face has one weight, so the browser fakes one, which is the thing the
/// handoff warns about (page 18).
/// </para>
/// </summary>
public class RestyleGateTests
{
    [Fact]
    public void DisplayFace_IsSelfHostedAndLicensed() // JJ-038
    {
        var root = RepoRoot();
        var fonts = Path.Combine(root, "src", "Shared.Ui", "wwwroot", "fonts");

        Assert.True(Directory.Exists(fonts), $"the RCL has no fonts folder: {fonts}");
        var faces = Directory.GetFiles(fonts, "*.woff2");
        Assert.NotEmpty(faces);
        Assert.True(File.Exists(Path.Combine(fonts, "OFL.txt")),
            "the display face ships without its licence — the OFL requires the text to travel with the font");

        // Small on purpose: the face is fetched on the login route, the first thing anyone sees.
        var total = faces.Sum(f => new FileInfo(f).Length);
        Assert.True(total < 60 * 1024, $"the display face weighs {total / 1024} KB across {faces.Length} files — subset it");

        var css = File.ReadAllText(Path.Combine(root, "src", "Shared.Ui", "wwwroot", "css", "app.css"));
        Assert.Contains("@font-face", css, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"src:\s*url\(""?\.\./fonts/[^)""]+\.woff2""?\)"), css);
        Assert.Contains("--font-display:", css, StringComparison.Ordinal);
    }

    [Fact]
    public void NoStylesheetOrHost_ReachesAFontHost() // JJ-038
    {
        var root = RepoRoot();
        var files = new[]
        {
            Path.Combine(root, "src", "Shared.Ui", "wwwroot", "css", "app.css"),
            Path.Combine(root, "src", "Web", "wwwroot", "index.html"),
            Path.Combine(root, "src", "Maui", "wwwroot", "index.html"),
        };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("fonts.googleapis.com", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fonts.gstatic.com", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DisplayFace_IsNeverAskedForBold() // JJ-038, handoff page 18
    {
        var root = RepoRoot();
        var razor = Directory.EnumerateFiles(Path.Combine(root, "src", "Shared.Ui"), "*.razor", SearchOption.AllDirectories);

        // Every class attribute that names the display face must not also name a bold utility. The
        // face has a single weight; a `fw-bold` beside it does not make it bolder, it makes the browser
        // draw a smeared copy of the regular.
        var classAttr = new Regex(@"class=""([^""]*)""");
        var offenders = new List<string>();
        foreach (var file in razor)
        {
            foreach (Match m in classAttr.Matches(File.ReadAllText(file)))
            {
                var classes = m.Groups[1].Value;
                if (classes.Contains("font-display", StringComparison.Ordinal)
                    && Regex.IsMatch(classes, @"\bfw-(bold|bolder|semibold)\b"))
                {
                    offenders.Add($"{Path.GetRelativePath(root, file)}: class=\"{classes}\"");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "The display face has one weight; drop the bold utility from: " + string.Join("; ", offenders));
    }

    [Fact]
    public void TheMark_ShipsAVariantForEachGround_AndTheRebrandChecklistKnows() // JJ-037, BACKBAR-2
    {
        var root = RepoRoot();
        var brand = Path.Combine(root, "src", "Shared.Ui", "wwwroot", "brand");

        // Two marks, one per ground: the copper one on the light surface, the bone one on the night
        // surface. A rebrand that replaces one and not the other ships a mark that vanishes in one
        // theme — which is why the checklist and the asset script both have to name both.
        Assert.True(File.Exists(Path.Combine(brand, "icon_light.svg")));
        Assert.True(File.Exists(Path.Combine(brand, "icon_dark.svg")), "icon_dark.svg is missing — the header has no mark on the dark surface");

        var css = File.ReadAllText(Path.Combine(root, "src", "Shared.Ui", "wwwroot", "css", "app.css"));
        Assert.Matches(new Regex(@"\[data-bs-theme=""dark""\]\s*\.brand-icon\s*\{[^}]*content:\s*url\(""?\.\./brand/icon_dark\.svg""?\)"), css);

        var rebranding = File.ReadAllText(Path.Combine(root, "docs", "REBRANDING.md"));
        Assert.Contains("icon_dark.svg", rebranding, StringComparison.Ordinal);

        var script = File.ReadAllText(Path.Combine(root, "docs", "brand", "build_assets.py"));
        Assert.Contains("icon_dark", script, StringComparison.Ordinal);
    }

    [Fact]
    public void NoCardBorder_SurvivesExceptTheCopperPanel() // BACKBAR-9, handoff page 18
    {
        var root = RepoRoot();
        var razor = Directory.EnumerateFiles(Path.Combine(root, "src", "Shared.Ui"), "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        // The definition of done: "no Bootstrap card border visible anywhere except the copper-subtle
        // panel". The panel is the unlock box on Home and Cocktails, and it is the only element allowed
        // to be a .card at all — everything else became a group, a section, a hairline row or a floating
        // panel with its own class. A bare `card` (or a card-header/card-footer, which only a card has)
        // anywhere else is the old app showing through.
        var classAttr = new Regex(@"class=""([^""]*)""");
        var offenders = new List<string>();
        foreach (var file in razor)
        {
            foreach (Match m in classAttr.Matches(File.ReadAllText(file)))
            {
                var classes = m.Groups[1].Value;
                var tokens = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var isCard = tokens.Any(t => t is "card" or "card-header" or "card-footer");
                var isThePanel = tokens.Any(t => t is "home-unlocks" or "catalog-unlocks");
                if (isCard && !isThePanel)
                    offenders.Add($"{Path.GetRelativePath(root, file)}: class=\"{classes}\"");
            }
        }

        Assert.True(offenders.Count == 0,
            "Only the copper-subtle unlock panel may be a .card (handoff page 18); restyle: " + string.Join("; ", offenders));
    }

    [Fact]
    public void EveryMotion_IsOptional() // BACKBAR-9, handoff page 18
    {
        var root = RepoRoot();
        var ui = Path.Combine(root, "src", "Shared.Ui");
        var sheets = new[] { Path.Combine(ui, "wwwroot", "css", "app.css") }
            .Concat(Directory.EnumerateFiles(ui, "*.razor.css", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        // "Motion is limited to 120ms fills and the 200ms panel expand, both dropped under
        // prefers-reduced-motion." Generalised: every selector that transitions or animates must be
        // named again inside a reduced-motion block in the SAME sheet, with the motion set to none.
        // Same sheet, because scoped CSS is rewritten per component and a rule in app.css cannot reach
        // a scoped selector — a reduced-motion block in the wrong file passes a reading and fails a user.
        var offenders = new List<string>();
        foreach (var sheet in sheets)
        {
            var text = Regex.Replace(File.ReadAllText(sheet), @"/\*.*?\*/", "", RegexOptions.Singleline);
            var (still, reduced) = SplitReducedMotion(text);

            var moving = Rules(still)
                .Where(r => Regex.IsMatch(r.body, @"(?<![\w-])(transition|animation)\s*:\s*(?!none\b)"))
                .SelectMany(r => r.selectors);
            var calmed = Rules(reduced)
                .Where(r => Regex.IsMatch(r.body, @"(?<![\w-])(transition|animation)\s*:\s*none\b"))
                .SelectMany(r => r.selectors)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var selector in moving.Distinct())
                if (!calmed.Contains(selector))
                    offenders.Add($"{Path.GetRelativePath(root, sheet)}: {selector}");
        }

        Assert.True(offenders.Count == 0,
            "These selectors move with no prefers-reduced-motion rule setting the motion to none: " + string.Join("; ", offenders));
    }

    [Fact]
    public void DarkThemeLinkColour_NeverOutranksAPagesOwnRule() // BACKBAR-9 amendment, 2026-09-14
    {
        var root = RepoRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "Shared.Ui", "wwwroot", "css", "app.css"));

        // Seen in the browser, not in a diff: in dark theme every drink name on Home and in the catalog
        // was copper, where the handoff draws them in the ink with copper on hover only (page 02, page
        // 07). The rule that paints links copper under dark excludes buttons with `:not()`, and `:not()`
        // carries the specificity of its argument — three of them took the selector to 0,4,1, which
        // outranks every scoped page rule (a class plus Blazor's scope attribute is 0,2,0). The
        // exclusions have to sit inside `:where()`, which contributes nothing, so the rule stays at the
        // 0,1,1 its own comment claims and a page's own colour wins.
        var dark = Regex.Match(css, @"\[data-bs-theme=""dark""\]\s*a(?<sel>[^,{]*)");
        Assert.True(dark.Success, "app.css has no dark-theme anchor rule");
        Assert.Matches(new Regex(@"^:where\(.*\)$"), dark.Groups["sel"].Value.Trim());
        Assert.DoesNotMatch(new Regex(@"\[data-bs-theme=""dark""\]\s*a:not\("), css);
    }

    [Fact]
    public void WholeScene_StretchesToTheRow_WhenThePanelIsTaller() // BACKBAR-5 amendment, 2026-09-14
    {
        var root = RepoRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "Shared.Ui", "Components", "MargaSplit.razor.css"));

        // Seen with OAuth providers configured, twice. First: two more buttons made the form taller than
        // the square, and her column (an aspect-ratio grid item, which `align-self: normal` does not
        // stretch) stopped short of the panel. Then the "fix": stretching it made the ratio take its
        // WIDTH from the row's height, and her column slid under the form. So the square is a floor —
        // a spacer whose padding resolves against the column's width — and never an aspect-ratio.
        var noRatio = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        Assert.DoesNotMatch(new Regex(@"\.split-whole\s+\.split-scene\s*\{[^}]*aspect-ratio"), noRatio);
        var spacer = Regex.Match(noRatio, @"\.split-whole\s+\.split-scene::before\s*\{([^}]*)\}");
        Assert.True(spacer.Success, "the whole scene has no square spacer");
        Assert.Matches(new Regex(@"padding-top:\s*100%"), spacer.Groups[1].Value);
    }

    [Fact]
    public void CatalogChips_ScrollRatherThanWrap_BelowLg() // BACKBAR-4 F7, amended 2026-09-14
    {
        var css = Regex.Replace(
            File.ReadAllText(Path.Combine(RepoRoot(), "src", "Shared.Ui", "Pages", "Cocktails.razor.css")),
            @"/\*.*?\*/", "", RegexOptions.Singleline);

        // F7 settled it: the chips carry their FULL labels at every width "and the row scrolls if it
        // must". It wrapped instead, so at tablet width "Everything · 971" dropped to a line of its own
        // under the other two — a radio group split across two rows reads as two controls.
        var media = Regex.Match(css, @"@media\s*\(max-width:\s*991\.98px\)\s*\{(.*?)\n\}", RegexOptions.Singleline);
        Assert.True(media.Success, "Cocktails.razor.css has no below-lg block");
        var scope = Regex.Match(media.Groups[1].Value, @"\.catalog-scope\s*\{([^}]*)\}");
        Assert.True(scope.Success, "the below-lg block has no .catalog-scope rule");
        Assert.Matches(new Regex(@"flex-wrap:\s*nowrap"), scope.Groups[1].Value);
        Assert.Matches(new Regex(@"overflow-x:\s*auto"), scope.Groups[1].Value);
    }

    [Fact]
    public void PrimaryButton_StaysCopperWhenDisabled() // BACKBAR-9
    {
        var root = RepoRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "Shared.Ui", "wwwroot", "css", "app.css"));

        // The visual pass found Rename, Invite, Transfer, Join and "Send to everyone" in Bootstrap's blue:
        // every one of them starts DISABLED, and a colour rule that names only the enabled states leaves
        // the disabled one to the framework's defaults. The rule has to set the disabled tokens too.
        var rule = Regex.Match(css, @"(?<![\w-])\.btn-primary\s*\{([^}]*)\}");
        Assert.True(rule.Success, "app.css has no .btn-primary rule");
        Assert.Contains("--bs-btn-disabled-bg", rule.Groups[1].Value, StringComparison.Ordinal);
        Assert.Contains("--bs-btn-disabled-border-color", rule.Groups[1].Value, StringComparison.Ordinal);
    }

    /// <summary>Lifts every <c>@media (prefers-reduced-motion: reduce)</c> block out of a sheet.</summary>
    private static (string still, string reduced) SplitReducedMotion(string css)
    {
        var still = new System.Text.StringBuilder();
        var reduced = new System.Text.StringBuilder();
        var media = new Regex(@"@media\s*\(\s*prefers-reduced-motion\s*:\s*reduce\s*\)\s*\{");
        var at = 0;
        foreach (Match m in media.Matches(css))
        {
            if (m.Index < at) continue;
            still.Append(css, at, m.Index - at);
            var depth = 1;
            var i = m.Index + m.Length;
            while (i < css.Length && depth > 0)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}') depth--;
                i++;
            }
            reduced.Append(css, m.Index + m.Length, i - 1 - (m.Index + m.Length)).Append('\n');
            at = i;
        }
        still.Append(css, at, css.Length - at);
        return (still.ToString(), reduced.ToString());
    }

    /// <summary>Innermost rules only: a selector list and its declaration block, whitespace collapsed.</summary>
    private static IEnumerable<(string[] selectors, string body)> Rules(string css)
    {
        foreach (Match m in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selectorText = m.Groups[1].Value;
            if (selectorText.Contains('@')) continue; // an @keyframes/@media prelude sharing the prefix
            var selectors = selectorText.Split(',')
                .Select(s => Regex.Replace(s, @"\s+", " ").Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            yield return (selectors, m.Groups[2].Value);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root.");
    }
}
