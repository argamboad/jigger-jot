using System.Net.Http;
using Bunit;
using JiggerJot.Shared.Ui.Pages;
using JiggerJot.Ui.Tests.Infrastructure;
using Xunit;

namespace JiggerJot.Ui.Tests;

/// <summary>
/// BACKBAR-7, Household (handoff page 17, JJ-039): six cards become four groups in two columns; row
/// actions are text links; Owner is the only copper-tinted badge; the household name renders in the
/// serif because it is a name; non-owners see the same layout with the owner's controls absent.
/// </summary>
public class HouseholdLayoutTests : ComponentTestBase
{
    private const string Other = "99999999-9999-9999-9999-999999999999";

    private string Roster(string myRole, string otherRole) => $$"""
        {"id":"11111111-1111-1111-1111-111111111111","name":"Gamboa household","my_role":"{{myRole}}",
         "members":[
           {"user_id":"{{Auth.UserId}}","display_name":"Allan Gamboa","email":"allan@example.com","role":"{{myRole}}","joined_at":"2026-01-01T00:00:00Z"},
           {"user_id":"{{Other}}","display_name":null,"email":"maria@example.com","role":"{{otherRole}}","joined_at":"2026-02-01T00:00:00Z"}]}
        """;

    private const string Pending = """
        [{"invitation_id":"22222222-2222-2222-2222-222222222222","invited_email":"sam@example.com","expires_at":"2026-09-26T00:00:00Z"}]
        """;

    private async Task<IRenderedComponent<Household>> RenderAsAsync(string myRole, string otherRole)
    {
        StubFeatures(billing: false);
        await SignInAsync();
        Http.On(HttpMethod.Get, "/api/household", Roster(myRole, otherRole));
        Http.On(HttpMethod.Get, "/api/household/invitations", Pending);
        var page = Render<Household>();
        page.WaitForAssertion(() => page.Find("[data-testid='member-row']"));
        return page;
    }

    [Fact]
    public async Task Owner_SeesFourGroups_TextLinkActions_AndOneCopperBadge()
    {
        var page = await RenderAsAsync("owner", "member");

        Assert.Empty(page.FindAll(".card"));
        Assert.NotNull(page.Find(".household-grid"));
        Assert.True(page.FindAll(".eyebrow").Count >= 4, "name, members, invitations, ownership & data");

        // Owner is the only copper-tinted badge; Admin and Member are neutral.
        Assert.Contains("bg-primary-subtle", page.Find("[data-testid='member-role'][data-role='owner']").ClassList);
        Assert.Contains("bg-secondary-subtle", page.Find("[data-testid='member-role'][data-role='member']").ClassList);

        // Row actions are text links, not outline buttons — a member row had three buttons
        // competing with the name. Same ids, same handlers.
        Assert.Contains("btn-link", page.Find("[data-testid='member-promote']").ClassList);
        Assert.Contains("btn-link", page.Find("[data-testid='member-remove']").ClassList);
        Assert.Contains("btn-link", page.Find("[data-testid='invite-revoke']").ClassList);
        Assert.Contains("btn-link", page.Find("[data-testid='invite-regenerate']").ClassList);
        Assert.Contains("btn-link", page.Find("[data-testid='export-data']").ClassList);

        // On a phone the actions collapse behind a ··· control; the actions themselves render ONCE
        // (the ids must not double), so the menu only toggles their visibility.
        var actionable = page.FindAll("[data-testid='member-row']").Single(r => r.QuerySelector("[data-testid='member-remove']") is not null);
        Assert.NotNull(actionable.QuerySelector("button.row-menu"));
        Assert.Single(page.FindAll("[data-testid='member-remove']"));
    }

    [Fact]
    public async Task Member_SeesTheNameInTheSerif_AndNoOwnerControls()
    {
        var page = await RenderAsAsync("member", "owner");

        Assert.Contains("font-display", page.Find(".household-name").ClassList);
        Assert.Equal("Gamboa household", page.Find(".household-name").TextContent.Trim());

        // Absent, not disabled.
        Assert.Empty(page.FindAll("[data-testid='household-rename-input']"));
        Assert.Empty(page.FindAll("[data-testid='member-remove'], [data-testid='member-promote'], [data-testid='member-demote']"));
        Assert.Empty(page.FindAll("[data-testid='invite-email']"));
        Assert.Empty(page.FindAll("[data-testid='transfer-select']"));

        // The roster stays; leaving is a red text link behind the existing confirm.
        Assert.Equal(2, page.FindAll("[data-testid='member-row']").Count);
        var leave = page.Find("[data-testid='leave-household']");
        Assert.Contains("btn-link", leave.ClassList);
        Assert.Contains("text-danger", leave.ClassList);
    }
}
