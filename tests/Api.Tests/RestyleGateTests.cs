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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root.");
    }
}
