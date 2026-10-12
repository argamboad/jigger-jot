using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// PREFS: the measurement preference behind <c>GET</c> / <c>PUT /api/unit-preference</c>, the UnitPreference slice
/// (Arch A3, jigger-jot#164 — it was <c>PUT /api/auth/unit-system</c> and a column on the platform's Users table).
/// CKTL-3 reads it; this is the half that makes conversion a choice a reader can actually make.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class UnitPreferenceTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task SetUnitSystem_Metric_IsStored()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);

        var response = await client.PutAsJsonAsync("/api/unit-preference", new { unitSystem = "Metric" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UnitSystem.Metric, await StoredAsync(user.UserId));
    }

    [Fact]
    public async Task SetUnitSystem_IsCaseInsensitive()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync("/api/unit-preference", new { unitSystem = "imperial" })).StatusCode);
        Assert.Equal(UnitSystem.Imperial, await StoredAsync(user.UserId));
    }

    [Fact]
    public async Task SetUnitSystem_Empty_IsRejected_BecauseThereIsNoAsWrittenAnyMore()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);
        await client.PutAsJsonAsync("/api/unit-preference", new { unitSystem = "Metric" });

        var response = await client.PutAsJsonAsync("/api/unit-preference", new { unitSystem = (string?)null });

        // JJ-041: every volume is stored in ounces, so "as written" would only ever mean "imperial".
        // Clearing the preference is refused rather than silently read as a choice nobody made.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(UnitSystem.Metric, await StoredAsync(user.UserId));
    }

    [Fact]
    public async Task SetUnitSystem_Neutral_IsRejected()
    {
        var client = factory.CreateClientFor(await factory.SeedUserAsync());

        // Neutral is a property of a UNIT, not something a reader can prefer: "show me everything in
        // dashes" is not a request anyone can act on, and accepting it would silently do nothing.
        var response = await client.PutAsJsonAsync("/api/unit-preference", new { unitSystem = "Neutral" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetUnitSystem_Nonsense_IsRejected()
    {
        var client = factory.CreateClientFor(await factory.SeedUserAsync());

        var response = await client.PutAsJsonAsync("/api/unit-preference", new { unitSystem = "furlongs" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetUnitSystem_WithoutAToken_IsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/unit-preference", new { unitSystem = "Metric" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_CarriesWhatTheReaderReads_ImperialWhenNeverChosen()
    {
        var client = factory.CreateClientFor(await factory.SeedUserAsync());

        // A reader who never chose reads ounces, because ounces are what is stored (JJ-041). The
        // slice says so, rather than leaving every client to know that "no row" means imperial.
        var before = await client.GetFromJsonAsync<Preference>("/api/unit-preference");
        Assert.Equal(("Imperial", true), (before!.UnitSystem, before.IsDefault));

        await client.PutAsJsonAsync("/api/unit-preference", new { unitSystem = "Metric" });

        var after = await client.GetFromJsonAsync<Preference>("/api/unit-preference");
        Assert.Equal(("Metric", false), (after!.UnitSystem, after.IsDefault));
    }

    private async Task<UnitSystem?> StoredAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.UserUnitPreferences.Where(p => p.UserId == userId)
            .Select(p => (UnitSystem?)p.UnitSystem)
            .SingleOrDefaultAsync();
    }

    private record Preference(
        [property: System.Text.Json.Serialization.JsonPropertyName("unitSystem")] string UnitSystem,
        [property: System.Text.Json.Serialization.JsonPropertyName("isDefault")] bool IsDefault);
}
