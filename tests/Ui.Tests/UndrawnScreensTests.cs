using System.Net.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Shared.Ui.Components;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-8 (JJ-039 — all pages, no exceptions): the screens the handoff never drew, restyled to
/// the same language by analogy with pages 02 and 16–17. Billing's four cards become two groups;
/// Admin's six cards and a table become groups with tenants as rows; not-found is an empty state.
/// Same calls, same gates, same ids; no admin write added (ADR-021).
/// </summary>
public class UndrawnScreensTests : ComponentTestBase
{
    private const string ProSummary = """
        {"plan_key":"pro","status":"active","current_period_end":"2026-10-13T00:00:00Z",
         "seats":{"used":3,"limit":10},"has_subscription":true}
        """;

    private const string Tenants = """
        [{"id":"11111111-1111-1111-1111-111111111111","name":"Gamboa household","member_count":2,"created_at":"2026-01-01T00:00:00Z"},
         {"id":"22222222-2222-2222-2222-222222222222","name":"Ada's bar","member_count":1,"created_at":"2026-02-01T00:00:00Z"}]
        """;

    private const string Detail = """
        {"id":"11111111-1111-1111-1111-111111111111","name":"Gamboa household","created_at":"2026-01-01T00:00:00Z",
         "members":[{"user_id":"33333333-3333-3333-3333-333333333333","display_name":"Allan","email":"allan@example.com","role":"owner"}],
         "subscription_status":"none","plan_key":"free","provider_managed":false,"audit_event_count":4}
        """;

    [Fact]
    public async Task Billing_IsTwoGroups_WithThePlanAsAPill()
    {
        StubFeatures(billing: true);
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/billing", ProSummary);

        var page = Render<Billing>();

        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll(".card"));
            Assert.NotNull(page.Find("h1.page-title"));
            Assert.True(page.FindAll(".settings-group").Count >= 2, "plan and seats are two labelled groups");

            // The plan name in the serif; the status a pill in the status colours, not info-blue.
            Assert.Contains("font-display", page.Find("[data-testid='billing-plan']").ClassList);
            var status = page.Find("[data-testid='billing-status']");
            Assert.DoesNotContain("bg-info-subtle", status.ClassList);
            Assert.Contains("badge", status.ClassList);

            // Same ids, same gate: a Pro plan shows Manage and never Upgrade.
            Assert.NotNull(page.Find("[data-testid='billing-renews']"));
            Assert.NotNull(page.Find("[data-testid='billing-seats']"));
            Assert.NotNull(page.Find("[data-testid='billing-portal']"));
            Assert.Empty(page.FindAll("[data-testid='billing-upgrade']"));
        });
    }

    [Fact]
    public async Task Admin_TenantsAreRows_NotTableCells_AndTheGateHolds()
    {
        StubFeatures(billing: true);
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/admin/me", """{"is_staff":true}""");
        Http.On(HttpMethod.Get, "/api/admin/tenants", Tenants);
        Http.On(HttpMethod.Get, "/api/admin/tenants/11111111-1111-1111-1111-111111111111", Detail);

        var page = Render<AdminConsole>();

        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll(".card"));
            Assert.NotNull(page.Find("h1.page-title"));
            var rows = page.FindAll("[data-testid='admin-tenant-row']");
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Contains("settings-row", r.ClassList));
            Assert.Empty(page.FindAll("table"));
        });

        page.Find("[data-testid='admin-tenant-row']").Click();

        page.WaitForAssertion(() =>
        {
            // The detail as labelled groups; members as rows; the plan as a neutral pill.
            Assert.NotNull(page.Find("[data-testid='admin-plan']"));
            Assert.NotNull(page.Find("[data-testid='admin-member-check']"));
            Assert.Empty(page.FindAll("table"));
            Assert.Empty(page.FindAll(".card"));
            Assert.Contains("btn-link", page.Find("[data-testid='admin-comp-pro']").ClassList);
            Assert.Contains("btn-link", page.Find("[data-testid='admin-mfa-reset']").ClassList);
        });
    }

    [Fact]
    public async Task Admin_NonStaff_SeesOnlyTheRefusal()
    {
        StubFeatures(billing: true);
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/admin/me", """{"is_staff":false}""");

        var page = Render<AdminConsole>();

        page.WaitForAssertion(() =>
        {
            Assert.NotNull(page.Find("[data-testid='admin-forbidden']"));
            Assert.Empty(page.FindAll("[data-testid='admin-tenant-row']"));
        });
    }

    [Fact]
    public void NotFound_IsAnEmptyState_NotABlankPage()
    {
        var cut = Render<NotFoundView>();

        Assert.NotNull(cut.Find("img.empty-scene"));
        Assert.NotNull(cut.Find(".catalog-empty-line.font-display"));
        Assert.NotNull(cut.Find("a[href='/']"));
        Assert.Empty(cut.FindAll(".display-1"));
    }
}
