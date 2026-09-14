using System.Globalization;
using System.Net;
using System.Resources;
using JiggerJot.Core.Abstractions;

namespace JiggerJot.Infrastructure.Email;

/// <summary>A rendered branded email: localized subject, HTML, and the inline images it references.</summary>
public sealed record EmailBody(string Subject, string Html, IReadOnlyList<EmailInlineImage> InlineImages);

/// <summary>
/// Builds the JiggerJot-branded HTML for transactional emails. Email HTML is its own world —
/// table-based layout, inline styles — so this does NOT reuse the app's CSS; it copies the app's
/// values instead, and <c>EmailLookTests</c> holds the copy to them. The lockup and Marga are embedded
/// via CID (multipart/related), the one approach Gmail and Outlook render reliably (they block
/// data-URI images).
/// <para>
/// <b>The look is the app's light set (BACKBAR, 2026-09-14):</b> the warm ground, white card on a
/// hairline, ink and muted text, copper for the one action, the display serif for the heading and
/// Marga's line, the sans stack for everything else, pill buttons and 12px fields. Light only, and it
/// says so to the client (<c>color-scheme: light only</c>): mail clients disagree on dark mode, and a
/// half-inverted email is worse than a light one. The serif is <b>embedded as data</b>, never linked —
/// a font URL in an email is a request to a server on open — and clients that drop the face (Gmail,
/// Outlook on Windows) fall back to Georgia, which is the app's own fallback.
/// </para>
/// <para>
/// Localized per <paramref name="culture"/> from <c>EmailStrings.resx</c>: emails are sent
/// server-side in an explicit culture (the requester's UI language for OTP/magic link, the
/// inviter's saved locale for invites), so it uses <see cref="ResourceManager"/> keyed by
/// culture rather than the ambient thread culture.
/// </para>
/// </summary>
public static class BrandedEmail
{
    private const string LockupCid = "jiggerjot-lockup";
    private const string MargaCid = "jiggerjot-marga";

    // The light set, handoff page 01, as app.css states it (JJ-036). Email HTML can't use CSS
    // variables, so these are literal copies of the tokens.
    private const string Ground = "#F6F3EF";   // --app-bg
    private const string Surface = "#FFFFFF";  // --surface
    private const string Hairline = "#E4D8CF"; // --app-border
    private const string Ink = "#1E1B18";      // --ink
    private const string Muted = "#6B635A";    // --muted
    private const string Copper = "#B4562A";   // --bs-primary, also --label-accent on light
    private const string Sans = "'Helvetica Neue',Helvetica,Arial,sans-serif";         // app.css body
    private const string Serif = "'Instrument Serif',Georgia,'Times New Roman',serif"; // --font-display

    private static readonly ResourceManager Rm =
        new("JiggerJot.Infrastructure.Email.EmailStrings", typeof(BrandedEmail).Assembly);
    private static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en");

    /// <summary>Resolves a locale code (e.g. "es") to a culture, defaulting to English.</summary>
    public static CultureInfo ResolveCulture(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return DefaultCulture;
        try { return CultureInfo.GetCultureInfo(locale.Trim()); }
        catch (CultureNotFoundException) { return DefaultCulture; }
    }

    private static string T(string key, CultureInfo culture, params object[] args)
    {
        var value = Rm.GetString(key, culture) ?? key;
        return args.Length == 0 ? value : string.Format(culture, value, args);
    }

    /// <summary>The header lockup as an inline image; reference it from HTML as <c>cid:jiggerjot-lockup</c>.</summary>
    public static EmailInlineImage Lockup() => new(LockupCid, "lockup.png", LoadAsset("lockup.png"), "image/png");

    /// <summary>Marga, as an inline image — attached only by the emails that actually show her.</summary>
    public static EmailInlineImage Marga() => new(MargaCid, "marga.png", LoadAsset("marga.png"), "image/png");

    /// <summary>"Email me a 6-digit code" — the OTP code email.</summary>
    public static EmailBody Otp(string code, int lifespanMinutes, CultureInfo culture) => Compose(
        T("Otp_Subject", culture),
        T("Otp_Preheader", culture, code),
        $"""
         {Heading(T("Otp_Heading", culture))}
         {MargaSays(T("Marga_SignIn", culture))}
         {Paragraph(T("Otp_Body", culture, lifespanMinutes))}
         <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:4px 0 4px;">
           <div style="display:inline-block;background:{Surface};border:1px solid {Hairline};border-radius:12px;padding:14px 16px 14px 26px;font-family:{Sans};font-size:28px;font-weight:500;letter-spacing:.35em;color:{Ink};">{code}</div>
         </td></tr></table>
         {IgnoreNote(T("Common_IgnoreNote", culture))}
         """, culture, withMarga: true);

    /// <summary>"Email me a magic link" — the passwordless sign-in link.</summary>
    public static EmailBody MagicLink(string link, int lifespanMinutes, CultureInfo culture) => Compose(
        T("MagicLink_Subject", culture),
        T("MagicLink_Preheader", culture),
        $"""
         {Heading(T("MagicLink_Heading", culture))}
         {MargaSays(T("Marga_SignIn", culture))}
         {Paragraph(T("MagicLink_Body", culture, lifespanMinutes))}
         {Button(T("MagicLink_Button", culture), link)}
         {Paragraph(T("MagicLink_OrPaste", culture), small: true)}
         <p style="margin:0 0 8px;font-family:{Sans};font-size:12px;line-height:1.5;color:{Muted};word-break:break-all;">{link}</p>
         {IgnoreNote(T("Common_IgnoreNote", culture))}
         """, culture, withMarga: true);

    /// <summary>Household invitation — join link plus the raw token fallback.</summary>
    public static EmailBody Invitation(string joinUrl, string token, CultureInfo culture) => Compose(
        T("Invitation_Subject", culture),
        T("Invitation_Preheader", culture),
        $"""
         {Heading(T("Invitation_Heading", culture))}
         {MargaSays(T("Marga_Invitation", culture))}
         {Paragraph(T("Invitation_Body", culture))}
         {Button(T("Invitation_Button", culture), joinUrl)}
         {Paragraph(T("Invitation_OrToken", culture), small: true)}
         <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:0 0 4px;">
           <div style="display:inline-block;background:{Surface};border:1px solid {Hairline};border-radius:12px;padding:10px 16px;font-family:'Courier New',monospace;font-size:13px;color:{Ink};word-break:break-all;">{token}</div>
         </td></tr></table>
         {IgnoreNote(T("Common_IgnoreNoteUnexpected", culture))}
         """, culture, withMarga: true);

    /// <summary>
    /// A branded in-app notification copy (NOTIFY-2). <paramref name="title"/>/<paramref name="body"/>
    /// are dynamic user-facing content, so they are HTML-encoded before interpolation — email content
    /// can't inject markup. The subject is the (plain) title.
    /// </summary>
    public static EmailBody Notification(string title, string body, CultureInfo culture) => Compose(
        title,
        WebUtility.HtmlEncode(title),
        $"""
         {Heading(WebUtility.HtmlEncode(title))}
         {Paragraph(WebUtility.HtmlEncode(body))}
         {IgnoreNote(T("Common_IgnoreNote", culture))}
         """, culture);

    // ── shell + pieces ────────────────────────────────────────────────────────

    private static EmailBody Compose(string subject, string preheader, string inner, CultureInfo culture, bool withMarga = false) =>
        new(subject, Wrap(preheader, inner, culture), withMarga ? [Lockup(), Marga()] : [Lockup()]);

    /// <summary>
    /// Marga, saying one line, in an email (MARGA-4), in the app's card tone (page 02): her name as the
    /// small copper label above the line, the line in the display face at 23px in the ink, her face at
    /// 72. The same contract as her component in the app: she is handed a FINISHED sentence and never
    /// builds, picks or fetches one.
    /// </summary>
    /// <remarks>
    /// Tables rather than a flex row, because email HTML has no flexbox worth the name;
    /// <c>valign="top"</c> and a fixed-width first cell are what keep Outlook from stacking them.
    /// The avatar is decorative — the line beside it carries the whole meaning — so it has an empty
    /// alt, which also means the block still reads correctly in the many clients that block images
    /// by default: the sentence is there, she simply is not.
    /// </remarks>
    private static string MargaSays(string line) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 24px;">
          <tr>
            <td width="88" valign="top" style="width:88px;padding-right:16px;">
              <img src="cid:{MargaCid}" width="72" height="72" alt="" style="display:block;border:0;outline:none;text-decoration:none;border-radius:36px;">
            </td>
            <td valign="top">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                <tr><td style="padding:4px 0 6px;font-family:{Sans};font-size:11px;font-weight:500;letter-spacing:.16em;text-transform:uppercase;color:{Copper};">Marga</td></tr>
                <tr><td class="jj-serif" style="font-family:{Serif};font-size:23px;line-height:1.25;font-weight:400;color:{Ink};">
                  {line}
                </td></tr>
              </table>
            </td>
          </tr>
        </table>
        """;

    private static string Wrap(string preheader, string inner, CultureInfo culture) => $$"""
        <!DOCTYPE html>
        <html lang="{{culture.TwoLetterISOLanguageName}}"><head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <meta name="x-apple-disable-message-reformatting">
        <meta name="color-scheme" content="light only">
        <meta name="supported-color-schemes" content="light">
        <title>JiggerJot</title>
        <style>
        @font-face { font-family:'Instrument Serif'; font-style:normal; font-weight:400; src:url(data:font/woff2;base64,{{DisplayFace.Value}}) format('woff2'); }
        :root { color-scheme: light only; }
        a { color: {{Copper}}; }
        @media (max-width: 520px) { .jj-card { padding: 28px 20px !important; } }
        </style>
        <!--[if mso]><style>h1, .jj-serif { font-family: Georgia, serif !important; }</style><![endif]-->
        </head>
        <body style="margin:0;padding:0;background:{{Ground}};">
        <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:{{Ground}};">{{preheader}}</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{{Ground}};">
        <tr><td align="center" style="padding:32px 16px;">
          <table role="presentation" cellpadding="0" cellspacing="0" style="width:100%;max-width:520px;">
            <tr><td align="center" style="padding:4px 0 24px;">
              <img src="cid:{{LockupCid}}" width="200" height="52" alt="JiggerJot" style="display:block;border:0;outline:none;text-decoration:none;font-family:{{Sans}};font-size:20px;font-weight:700;color:{{Ink}};">
            </td></tr>
            <tr><td class="jj-card" style="background:{{Surface}};border:1px solid {{Hairline}};border-radius:16px;padding:36px 32px;">
              {{inner}}
            </td></tr>
            <tr><td align="center" style="padding:24px 8px 0;font-family:{{Sans}};font-size:12px;line-height:1.6;color:{{Muted}};">
              JiggerJot &middot; Mix what you have.
            </td></tr>
          </table>
        </td></tr>
        </table>
        </body></html>
        """;

    private static string Heading(string text) =>
        $"""<h1 class="jj-serif" style="margin:0 0 16px;font-family:{Serif};font-size:29px;line-height:1.25;font-weight:400;color:{Ink};">{text}</h1>""";

    private static string Paragraph(string text, bool small = false) =>
        $"""<p style="margin:0 0 {(small ? "8" : "24")}px;font-family:{Sans};font-size:{(small ? "13" : "15")}px;line-height:1.6;color:{(small ? Muted : Ink)};">{text}</p>""";

    // A pill, as every button in the app is. Bulletproof-ish (table + bgcolor) so Outlook still paints
    // the copper — Outlook on Windows ignores the radius and draws it square, which is the one concession.
    private static string Button(string label, string href) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:4px auto 24px;"><tr>
          <td align="center" bgcolor="{Copper}" style="border-radius:999px;">
            <a href="{href}" style="display:inline-block;padding:13px 32px;font-family:{Sans};font-size:15px;font-weight:500;color:#ffffff;text-decoration:none;border-radius:999px;">{label}</a>
          </td>
        </tr></table>
        """;

    // On a hairline, like the app's rules: the note is furniture, not part of the message.
    private static string IgnoreNote(string text) =>
        $"""<p style="margin:28px 0 0;padding-top:18px;border-top:1px solid {Hairline};font-family:{Sans};font-size:13px;line-height:1.5;color:{Muted};">{text}</p>""";

    /// <summary>The RCL's latin subset of the display face, base64 once per process (~28 KB of text).</summary>
    private static readonly Lazy<string> DisplayFace =
        new(() => Convert.ToBase64String(LoadAsset("instrument-serif-latin.woff2")));

    private static readonly Dictionary<string, byte[]> AssetCache = [];

    private static byte[] LoadAsset(string fileName)
    {
        lock (AssetCache)
        {
            if (AssetCache.TryGetValue(fileName, out var cached)) return cached;

            var asm = typeof(BrandedEmail).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Embedded email asset ({fileName}) not found.");
            using var stream = asm.GetManifestResourceStream(name)!;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return AssetCache[fileName] = memory.ToArray();
        }
    }
}
