using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// PREFS: the measurement preference behind <c>PUT /api/auth/unit-system</c>. CKTL-3 reads
/// <c>PreferredUnitSystem</c> but nothing set it; this is the half that makes conversion a choice a
/// reader can actually make rather than a column nobody fills in.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class UnitPreferenceTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task SetUnitSystem_Metric_IsStored()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);

        var response = await client.PutAsJsonAsync("/api/auth/unit-system", new { unitSystem = "Metric" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UnitSystem.Metric, await StoredAsync(user.UserId));
    }

    [Fact]
    public async Task SetUnitSystem_IsCaseInsensitive()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync("/api/auth/unit-system", new { unitSystem = "imperial" })).StatusCode);
        Assert.Equal(UnitSystem.Imperial, await StoredAsync(user.UserId));
    }

    [Fact]
    public async Task SetUnitSystem_Empty_ClearsBackToAsWritten()
    {
        var user = await factory.SeedUserAsync();
        var client = factory.CreateClientFor(user);
        await client.PutAsJsonAsync("/api/auth/unit-system", new { unitSystem = "Metric" });

        var response = await client.PutAsJsonAsync("/api/auth/unit-system", new { unitSystem = (string?)null });

        // "Never chose" is a real state a reader can return to, not just where they started. Without
        // this there would be no way back to reading the 1930 book in the book's own words.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await StoredAsync(user.UserId));
    }

    [Fact]
    public async Task SetUnitSystem_Neutral_IsRejected()
    {
        var client = factory.CreateClientFor(await factory.SeedUserAsync());

        // Neutral is a property of a UNIT, not something a reader can prefer: "show me everything in
        // dashes" is not a request anyone can act on, and accepting it would silently do nothing.
        var response = await client.PutAsJsonAsync("/api/auth/unit-system", new { unitSystem = "Neutral" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetUnitSystem_Nonsense_IsRejected()
    {
        var client = factory.CreateClientFor(await factory.SeedUserAsync());

        var response = await client.PutAsJsonAsync("/api/auth/unit-system", new { unitSystem = "furlongs" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetUnitSystem_WithoutAToken_IsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/auth/unit-system", new { unitSystem = "Metric" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_CarriesThePreference_AndNullWhenNeverChosen()
    {
        var client = factory.CreateClientFor(await factory.SeedUserAsync());

        var before = await client.GetFromJsonAsync<Profile>("/api/auth/me");
        Assert.Null(before!.PreferredUnitSystem);

        await client.PutAsJsonAsync("/api/auth/unit-system", new { unitSystem = "Imperial" });

        var after = await client.GetFromJsonAsync<Profile>("/api/auth/me");
        // Null travels as null rather than being collapsed to a default, because the switcher offers
        // "as written" as a choice and needs to know which one the reader is on.
        Assert.Equal("Imperial", after!.PreferredUnitSystem);
    }

    private async Task<UnitSystem?> StoredAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Id == userId)
            .Select(u => u.PreferredUnitSystem)
            .SingleAsync();
    }

    private record Profile(string? PreferredUnitSystem);
}
