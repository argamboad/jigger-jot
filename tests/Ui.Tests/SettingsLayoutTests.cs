using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Components;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-7, Settings (handoff page 16, JJ-039): five cards become labelled groups of hairline rows
/// in two columns; Theme and Measurements become segmented pills that write the same preference the
/// selects did; the danger zone is one red text link; every id and every call survives.
/// </summary>
public class SettingsLayoutTests : ComponentTestBase
{
    private async Task<IRenderedComponent<Settings>> RenderSettingsAsync()
    {
        StubFeatures(billing: true);
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/auth/providers", """{"providers":["google","microsoft"]}""");
        Http.On(HttpMethod.Get, "/api/auth/logins", """[{"provider":"google"}]""");
        Http.On(HttpMethod.Get, "/api/notifications/preferences", """{"inApp":true,"email":false}""");
        Http.On(HttpMethod.Get, "/api/auth/mfa", """{"enabled":false}""");
        Http.On(HttpMethod.Get, "/api/auth/me", """{"preferredUnitSystem":null}""");
        var page = Render<Settings>();
        // Wait for the provider rows, which arrive after the probe and the logins load.
        page.WaitForAssertion(() => page.Find(".settings-row"));
        return page;
    }

    [Fact]
    public async Task GroupsOfRows_InTwoColumns_NotCards()
    {
        var page = await RenderSettingsAsync();

        Assert.Empty(page.FindAll(".card"));
        Assert.NotNull(page.Find(".settings-grid"));
        Assert.NotNull(page.Find("h1.page-title"));
        Assert.True(page.FindAll(".eyebrow").Count >= 4, "sign-in, two-factor, preferences and notifications are labelled groups");

        // The provider rows are hairlines with text-link actions, not outline buttons.
        Assert.NotNull(page.Find(".settings-row"));
        Assert.Empty(page.FindAll(".btn-outline-danger, .btn-outline-primary"));
    }

    [Fact]
    public async Task ThemeAndMeasurements_AreSegmented_LanguageStaysASelect()
    {
        var page = await RenderSettingsAsync();

        foreach (var value in new[] { "system", "light", "dark" })
        {
            var radio = page.Find($"#theme-choice-{value}");
            Assert.Equal("radio", radio.GetAttribute("type"));
            Assert.Equal("theme-choice", radio.GetAttribute("name"));
        }
        foreach (var value in new[] { "AsWritten", "Metric", "Imperial" })
        {
            var radio = page.Find($"#unit-choice-{value}");
            Assert.Equal("radio", radio.GetAttribute("type"));
            Assert.Equal("unit-choice", radio.GetAttribute("name"));
        }

        // The header's select keeps `theme-switcher`; the page no longer renders a second one under
        // the same id, which was two identical ids on one page waiting for a journey to find them.
        Assert.Empty(page.FindAll("[data-testid='theme-switcher']"));
        Assert.Equal("select", page.Find("[data-testid='language-switcher']").TagName.ToLowerInvariant());
    }

    [Fact]
    public async Task DeleteMyAccount_IsARedTextLink_NotABox()
    {
        var page = await RenderSettingsAsync();

        var delete = page.Find("[data-testid='delete-account']");
        Assert.Contains("btn-link", delete.ClassList);
        Assert.Contains("text-danger", delete.ClassList);
        Assert.Empty(page.FindAll(".border-danger-subtle"));
    }

    [Fact]
    public async Task TheSegmentedUnitControl_WritesTheSamePreference()
    {
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/auth/me", """{"preferredUnitSystem":null}""");
        Http.On(HttpMethod.Put, "/api/auth/unit-system", "{}");

        var cut = Render<UnitSwitcher>(ps => ps.Add(p => p.Segmented, true));
        cut.WaitForAssertion(() => cut.Find("#unit-choice-Imperial"));

        await cut.Find("#unit-choice-Imperial").ChangeAsync(new() { Value = "Imperial" });

        cut.WaitForAssertion(() => Assert.Contains(Http.Requests, r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == "/api/auth/unit-system"));
        Assert.True(cut.Find("#unit-choice-Imperial").HasAttribute("checked"));
    }

    [Fact]
    public async Task TheSegmentedThemeControl_WritesTheSamePreference()
    {
        await SignInAsync();
        Http.On(HttpMethod.Put, "/api/auth/theme", "{}");

        var cut = Render<ThemeSwitcher>(ps => ps.Add(p => p.Segmented, true));
        await cut.Find("#theme-choice-dark").ChangeAsync(new() { Value = "dark" });

        Assert.Equal("dark", await ThemeStore.GetAsync());
        cut.WaitForAssertion(() => Assert.Contains(Http.Requests, r =>
            r.Method == HttpMethod.Put && r.RequestUri!.AbsolutePath == "/api/auth/theme"));
    }
}
