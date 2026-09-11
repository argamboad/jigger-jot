using System.Text.Json;
using System.Text.RegularExpressions;

namespace JiggerJot.Api.Tests;

/// <summary>
/// v3 audit T60 (Group L): the last three [machine] rules from FOUNDATION_RULES_v2 that had no
/// standing gate — promoted here so every machine rule is enforced, not aspirational.
/// R61: one SDK pin source (global.json / Dockerfile tags / CI all agree — the DEP-4 drift that
/// caused recurring red builds). R68: host parity (both index.html files load the identical RCL
/// script set — a divergence ships a web-only or native-only breakage, NATIVE_PARITY rule).
/// R75: doc-map + QA-count sync (every top-level docs/*.md is in the CLAUDE.md map; the "N cases"
/// figure matches the QA plan — the drift T54 just hand-fixed, now machine-held).
/// </summary>
public class EnforcementGateTests
{
    [Fact]
    public void SdkPin_HasOneSource_AllConsumersAgree() // R61
    {
        var root = RepoRoot();

        // global.json is THE source (v3 DEP-4 / CLAUDE.md bump-together playbook).
        using var globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        var sdk = globalJson.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;

        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        var buildTag = Regex.Match(dockerfile, @"dotnet/sdk:([0-9.]+)").Groups[1].Value;
        Assert.True(sdk == buildTag,
            $"Dockerfile build image (sdk:{buildTag}) != global.json ({sdk}) — bump them TOGETHER (CLAUDE.md playbook).");

        // The runtime tag must match the ASP.NET package line the app compiles against.
        var packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));
        var aspNetPackage = Regex.Match(packages, @"Microsoft\.AspNetCore\.Authentication\.JwtBearer""\s+Version=""([0-9.]+)""").Groups[1].Value;
        var runtimeTag = Regex.Match(dockerfile, @"dotnet/aspnet:([0-9.]+)").Groups[1].Value;
        Assert.True(aspNetPackage == runtimeTag,
            $"Dockerfile runtime image (aspnet:{runtimeTag}) != the ASP.NET package line ({aspNetPackage}).");

        // CI must read the SDK from global.json, never hardcode one.
        var ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        Assert.Contains("global-json-file: global.json", ci);
        Assert.DoesNotContain("dotnet-version:", ci); // a hardcoded setup-dotnet version would fork the pin
    }

    [Fact]
    public void HostIndexHtml_ReferenceTheIdenticalRclScriptSet() // R68
    {
        // The RCL's js contracts (theme pre-paint, MFA QR) must load in BOTH hosts — a script added
        // to one index.html only ships a host-specific breakage (NATIVE_PARITY maintainer rule:
        // "index.html sync"). Compare the full RCL script sets, not a hardcoded list.
        var root = RepoRoot();
        static HashSet<string> Scripts(string path) =>
            [.. Regex.Matches(File.ReadAllText(path), @"_content/[A-Za-z0-9./_-]+\.js").Select(m => m.Value)];

        var web = Scripts(Path.Combine(root, "src", "Web", "wwwroot", "index.html"));
        var maui = Scripts(Path.Combine(root, "src", "Maui", "wwwroot", "index.html"));
        Assert.NotEmpty(web); // probe alive

        var webOnly = web.Except(maui).ToList();
        var mauiOnly = maui.Except(web).ToList();
        Assert.True(webOnly.Count == 0 && mauiOnly.Count == 0,
            "The two hosts' index.html RCL script sets diverged — add the script to BOTH "
            + $"(web-only: [{string.Join(", ", webOnly)}], maui-only: [{string.Join(", ", mauiOnly)}]).");
    }

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

    [Fact]
    public void ClaudeMdDocMap_ListsEveryTopLevelDoc() // R75, doc-map half
    {
        var root = RepoRoot();
        var claudeMd = File.ReadAllText(Path.Combine(root, "CLAUDE.md"));

        var missing = Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.TopDirectoryOnly)
            .Select(f => "docs/" + Path.GetFileName(f))
            .Where(rel => !claudeMd.Contains(rel, StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0,
            "Top-level docs missing from the CLAUDE.md doc map — the map is what makes a doc visible "
            + $"to every session (v3 TR-1): {string.Join(", ", missing)}");
    }

    [Fact]
    public void ClaudeMdQaCaseCount_MatchesTheQaPlan() // R75, count half
    {
        var root = RepoRoot();
        var actual = Regex.Matches(File.ReadAllText(Path.Combine(root, "docs", "QA_TEST_PLAN.md")),
            @"(?m)^### QA-").Count;

        var claimMatch = Regex.Match(File.ReadAllText(Path.Combine(root, "CLAUDE.md")), @"\((\d+) cases:");
        Assert.True(claimMatch.Success, "CLAUDE.md no longer states the QA case count as '(N cases:' — update this gate with it.");
        var claimed = int.Parse(claimMatch.Groups[1].Value);

        Assert.True(claimed == actual,
            $"CLAUDE.md claims {claimed} QA cases but QA_TEST_PLAN.md defines {actual} — update the doc-map row "
            + "(the figure drifted twice before this gate existed: v3 TR-2, T54).");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root.");
    }
}
