using System.Globalization;
using JiggerJot.Infrastructure.Email;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// MARGA-4: Marga in the transactional emails.
/// <para>
/// The decision these tests hold is <b>where she does not go</b>. Three of the four emails are ones a
/// person asked for — a sign-in code, a sign-in link, an invitation — and she belongs on those. The
/// fourth wraps arbitrary system notifications, including a failed payment and a security alert, and
/// a bartender character on "your subscription is past due" undercuts the message. That is a
/// judgement, so it is written down as a test rather than left to whoever edits the template next.
/// </para>
/// </summary>
public class MargaInEmailTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es");

    private const string MargaCid = "cid:jiggerjot-marga";

    public static TheoryData<string, EmailBody> ShowsHer() => new()
    {
        { "otp", BrandedEmail.Otp("123456", 10, En) },
        { "magic link", BrandedEmail.MagicLink("https://example.test/x", 10, En) },
        { "invitation", BrandedEmail.Invitation("https://example.test/join", "tok", En) },
    };

    [Theory]
    [MemberData(nameof(ShowsHer))]
    public void TheEmailsSomeoneAskedFor_ShowHer(string which, EmailBody email)
    {
        Assert.Contains(MargaCid, email.Html);
        Assert.Contains(email.InlineImages, i => i.ContentId == "jiggerjot-marga");
        Assert.True(email.InlineImages.Count == 2, $"{which} should carry the logo and Marga");
    }

    [Fact]
    public void TheNotificationEmail_DoesNot()
    {
        // The one that carries "your subscription is past due" and "a new sign-in on your account".
        var email = BrandedEmail.Notification("Payment failed", "We could not take your payment.", En);

        Assert.DoesNotContain(MargaCid, email.Html);
        Assert.DoesNotContain(email.InlineImages, i => i.ContentId == "jiggerjot-marga");

        // ...and she is not merely hidden: 19 KB must not ride along on an email that never shows it.
        Assert.Single(email.InlineImages);
    }

    [Fact]
    public void HerAvatarIsDecorative_SoAnImageBlockingClientLosesNothing()
    {
        var email = BrandedEmail.Otp("123456", 10, En);

        // Empty alt, the same as her component in the app. Most clients block images by default, and
        // the sentence beside her carries the whole meaning — "photo of a bartender" ahead of it
        // would only get in the way, and a client that blocks the image must lose nothing but her.
        var tag = System.Text.RegularExpressions.Regex.Match(
            email.Html, $"""<img[^>]*{System.Text.RegularExpressions.Regex.Escape(MargaCid)}[^>]*>""");

        Assert.True(tag.Success, "her image tag was not found");
        Assert.Contains("alt=\"\"", tag.Value);

        // ...and the line survives without her.
        Assert.Contains("Tell me what's on your shelf", email.Html);
    }

    [Fact]
    public void SheSpeaksSpanish_AndItIsWritten_NotTranslated()
    {
        var english = BrandedEmail.Invitation("https://example.test/join", "tok", En);
        var spanish = BrandedEmail.Invitation("https://example.test/join", "tok", Es);

        Assert.Contains("behind the bar", english.Html);
        Assert.Contains("detrás de la barra", spanish.Html);

        // Both carry her, and the line differs — a missing translation would fall back to the key or
        // to the English, and both of those would pass a "contains Marga" check.
        Assert.Contains(MargaCid, spanish.Html);
        Assert.DoesNotContain("Marga_Invitation", spanish.Html);
        Assert.DoesNotContain("behind the bar", spanish.Html);
    }

    [Fact]
    public void HerLineIsAResourceString_NeverAssembled()
    {
        // The contract that keeps "not an assistant" true: every sentence is one whole string from
        // EmailStrings.resx. If a key is ever missing, the resolver echoes the KEY — which would ship
        // "Marga_SignIn" to a real inbox, so it is worth failing here instead.
        var email = BrandedEmail.MagicLink("https://example.test/x", 10, En);

        Assert.DoesNotContain("Marga_SignIn", email.Html);
        Assert.DoesNotContain("Marga_Invitation", email.Html);
    }
}
