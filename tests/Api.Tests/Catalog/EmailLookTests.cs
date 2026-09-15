using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using JiggerJot.Infrastructure.Email;
using Xunit;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// The transactional emails wear the app's own look (BACKBAR, 2026-09-14): the light token set, the
/// display serif for headings and her line, the sans stack for everything else, pill buttons, hairline
/// fields, and the lockup the login shows. Email HTML cannot share <c>app.css</c>, so every value is a
/// literal copy — which is exactly why these tests exist: a copy is the thing that drifts.
/// </summary>
public class EmailLookTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es");

    // The light set, handoff page 01, as app.css states it.
    private const string Ground = "#F6F3EF";
    private const string Hairline = "#E4D8CF";
    private const string Ink = "#1E1B18";
    private const string Muted = "#6B635A";
    private const string Copper = "#B4562A";
    private const string Sans = "'Helvetica Neue',Helvetica,Arial,sans-serif";

    public static TheoryData<string, EmailBody> Every() => new()
    {
        { "otp", BrandedEmail.Otp("123456", 10, En) },
        { "otp es", BrandedEmail.Otp("123456", 10, Es) },
        { "magic link", BrandedEmail.MagicLink("https://example.test/x", 10, En) },
        { "invitation", BrandedEmail.Invitation("https://example.test/join", "tok", En) },
        { "invitation es", BrandedEmail.Invitation("https://example.test/join", "tok", Es) },
        { "notification", BrandedEmail.Notification("Payment failed", "We could not take your payment.", En) },
    };

    [Theory]
    [MemberData(nameof(Every))]
    public void UsesTheAppsLightPalette_AndNoneOfTheRetiredGreys(string which, EmailBody email)
    {
        foreach (var token in new[] { Ground, Hairline, Ink, Muted, Copper, Sans })
            Assert.True(email.Html.Contains(token, StringComparison.OrdinalIgnoreCase), $"{which} is missing {token}");

        // The pre-restyle template's cool greys, its brass text tones and Segoe UI: none of them is in
        // the app any more, so none of them may be in its emails.
        foreach (var retired in new[] { "#F4F5F7", "#1B1F24", "#5B6470", "#9C6A3F", "#7A6E66", "Segoe UI" })
            Assert.False(email.Html.Contains(retired, StringComparison.OrdinalIgnoreCase), $"{which} still carries {retired}");
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void TheHeadingIsTheDisplayFace_NeverBold(string which, EmailBody email)
    {
        // JJ-038: one serif, one weight. A bold heading in it is a smeared regular.
        var h1 = Regex.Match(email.Html, @"<h1[^>]*style=""([^""]*)""");
        Assert.True(h1.Success, $"{which} has no heading");
        Assert.Contains("'Instrument Serif'", h1.Groups[1].Value);
        Assert.Contains("Georgia", h1.Groups[1].Value);
        Assert.Contains("font-weight:400", h1.Groups[1].Value);
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void TheDisplayFaceTravelsWithTheEmail_AndIsTheAppsOwnFile(string which, EmailBody email)
    {
        // Embedded as data, never fetched: a font URL in an email is a request from the reader's mail
        // client to a server, on open — a tracking pixel by another name, and JJ-038 self-hosts the face
        // precisely so nothing leaves the app for a font.
        var face = Regex.Match(email.Html, @"@font-face\s*\{[^}]*src:\s*url\(data:font/woff2;base64,([A-Za-z0-9+/=]+)\)[^}]*\}");
        Assert.True(face.Success, $"{which} does not embed the display face");
        Assert.DoesNotMatch(new Regex(@"@font-face\s*\{[^}]*https?:"), email.Html);
        Assert.DoesNotContain("fonts.googleapis", email.Html);

        // The same bytes the app ships, so the email copy cannot drift from the RCL's face — and its
        // licence travels beside it, as the OFL requires.
        var root = RepoRoot();
        var rcl = File.ReadAllBytes(Path.Combine(root, "src", "Shared.Ui", "wwwroot", "fonts", "instrument-serif-latin.woff2"));
        Assert.Equal(rcl, Convert.FromBase64String(face.Groups[1].Value));
        Assert.True(File.Exists(Path.Combine(root, "src", "Infrastructure", "Email", "Assets", "OFL.txt")));
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void ItFitsUnderGmailsClip(string which, EmailBody email)
    {
        // Gmail clips a message body past 102 KB behind "View entire message" — which would hide the
        // code or the button. The embedded face is the heavy part; the whole email stays well under.
        var bytes = Encoding.UTF8.GetByteCount(email.Html);
        Assert.True(bytes < 90_000, $"{which} is {bytes:N0} bytes of HTML");
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void OutlookOnWindows_GetsGeorgia_NotTimesNewRoman(string which, EmailBody email)
    {
        // Outlook's Word engine ignores the stack when the first family is missing and falls straight to
        // Times New Roman. A conditional block names Georgia outright for it.
        var mso = Regex.Match(email.Html, @"<!--\[if mso\]>(.*?)<!\[endif\]-->", RegexOptions.Singleline);
        Assert.True(mso.Success, $"{which} has no Outlook block");
        Assert.Contains("Georgia", mso.Groups[1].Value);
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void TheHeaderIsTheLockup_AndItAsksToStayLight(string which, EmailBody email)
    {
        // The lockup the login shows (ink "Jigger", copper "Jot", the tagline) rather than the bare mark
        // over a bold copper word; its alt carries the name for the clients that block images.
        Assert.Matches(new Regex(@"<img[^>]*src=""cid:jiggerjot-lockup""[^>]*alt=""JiggerJot"""), email.Html);
        Assert.Contains(email.InlineImages, i => i.ContentId == "jiggerjot-lockup");
        Assert.DoesNotContain("cid:jiggerjot-logo", email.Html);

        // The light set is the email's look; clients that honour this do not repaint it half-dark.
        Assert.Contains(@"<meta name=""color-scheme"" content=""light only"">", email.Html);
        Assert.False(string.IsNullOrEmpty(which));
    }

    [Fact]
    public void TheLockupIsTransparent_SoItSitsOnTheGround_NotInABox()
    {
        // The lockup sits on the email's warm ground, above the white card. Rendered flat on white it
        // drew a white rectangle around the name — seen in the first render. A PNG's colour type is byte
        // 25 of the file (inside IHDR): 6 is RGBA, 4 is grey with alpha.
        var png = BrandedEmail.Lockup().Content;
        Assert.True(png[25] is 6 or 4, $"lockup.png has colour type {png[25]}, which carries no alpha");
    }

    [Fact]
    public void TheLanguageAttributeFollowsTheCulture()
    {
        Assert.Contains(@"<html lang=""en""", BrandedEmail.Otp("123456", 10, En).Html);
        Assert.Contains(@"<html lang=""es""", BrandedEmail.Otp("123456", 10, Es).Html);
    }

    [Fact]
    public void ButtonsArePills_InCopper()
    {
        foreach (var email in new[]
                 {
                     BrandedEmail.MagicLink("https://example.test/x", 10, En),
                     BrandedEmail.Invitation("https://example.test/join", "tok", En),
                 })
        {
            var cell = Regex.Match(email.Html, $@"<td[^>]*bgcolor=""{Copper}""[^>]*style=""([^""]*)""[^>]*>\s*<a[^>]*style=""([^""]*)""");
            Assert.True(cell.Success, "no copper button");
            Assert.Contains("border-radius:999px", cell.Groups[1].Value);
            Assert.Contains("border-radius:999px", cell.Groups[2].Value);
        }
    }

    [Fact]
    public void Marga_SpeaksInHerCardTone()
    {
        // The app's card tone (page 02): her name as the small label ABOVE the line, the line in the
        // display face at 23px in the ink, her face at 72.
        var html = BrandedEmail.Otp("123456", 10, En).Html;

        var label = html.IndexOf(">Marga<", StringComparison.Ordinal);
        var line = html.IndexOf("Tell me what", StringComparison.Ordinal);
        Assert.True(label > 0 && line > label, "her label should come before her line");

        Assert.Matches(new Regex(@"<img[^>]*src=""cid:jiggerjot-marga""[^>]*width=""72"""), html);
        Assert.Matches(new Regex(@"<td[^>]*style=""[^""]*'Instrument Serif'[^""]*font-size:23px[^""]*""[^>]*>\s*Tell me what"), html);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root.");
    }
}
