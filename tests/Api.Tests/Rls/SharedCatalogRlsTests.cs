using Microsoft.EntityFrameworkCore;
using Npgsql;
using JiggerJot.Api.Tests.Catalog;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;

namespace JiggerJot.Api.Tests.Rls;

/// <summary>
/// The database half of JJ-031, and the reason the shared-or-tenant policy set is asymmetric. Connecting
/// as the non-privileged runtime role — the only way to observe RLS, since a superuser is exempt — these
/// prove that on a dual-natured table:
/// <list type="bullet">
/// <item>a household reads the shared catalog plus its own rows, and never another household's;</item>
/// <item>a household <b>cannot delete or update a shared row</b>, even though it can read it. A single
/// <c>FOR ALL</c> policy would have allowed exactly that, because <c>DELETE</c> is checked against
/// <c>USING</c> and <c>WITH CHECK</c> never applies to it — this suite is what keeps that mistake from
/// coming back;</item>
/// <item>writing a shared row requires the bypass GUC, so seeding the catalog is a deliberate,
/// greppable act rather than something a slice can do by leaving <c>TenantId</c> unset.</item>
/// </list>
/// Nothing here relies on the EF query filter; <see cref="SharedCatalogFilterTests"/> covers that wall
/// separately, which is the point of having two.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SharedCatalogRlsTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly Guid _mine = Guid.CreateVersion7();
    private readonly Guid _theirs = Guid.CreateVersion7();
    private string RuntimeCs => RlsTestSetup.RuntimeConnectionString(fixture.ConnectionString);

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateTestContext();
        await RlsTestSetup.ProvisionAsync(db);

        // Seeded as the (RLS-exempt) superuser with no ambient tenant and explicit owners — nothing
        // stamps an ISharedOrTenantScoped row.
        var lookups = await CatalogSeed.LookupsAsync(db);
        await CatalogSeed.IngredientAsync(db, lookups, "shared gin", tenantId: null);
        await CatalogSeed.IngredientAsync(db, lookups, "house infusion", _mine);
        await CatalogSeed.IngredientAsync(db, lookups, "their infusion", _theirs);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Select_SeesSharedAndOwn_NeverAnotherHousehold()
    {
        var names = await AsHouseholdAsync(_mine, async (conn, tx) =>
        {
            await using var cmd = new NpgsqlCommand("""SELECT "Name" FROM "Ingredients" ORDER BY "Name";""", conn, tx);
            await using var reader = await cmd.ExecuteReaderAsync();
            var found = new List<string>();
            while (await reader.ReadAsync()) found.Add(reader.GetString(0));
            return found;
        });

        Assert.Equal(["house infusion", "shared gin"], names);
    }

    [Fact]
    public async Task Delete_CannotRemoveASharedCatalogRow()
    {
        var deleted = await AsHouseholdAsync(_mine, async (conn, tx) =>
        {
            await using var cmd = new NpgsqlCommand(
                """DELETE FROM "Ingredients" WHERE "Name" = 'shared gin';""", conn, tx);
            return await cmd.ExecuteNonQueryAsync();
        });

        // Not an error — the DELETE policy simply never matches the row, so it affects nothing. That is
        // the read-only-catalog rule (JJ-002) enforced by Postgres.
        Assert.Equal(0, deleted);
        Assert.True(await SharedGinStillExistsAsync());
    }

    [Fact]
    public async Task Update_CannotRewriteASharedCatalogRow()
    {
        var updated = await AsHouseholdAsync(_mine, async (conn, tx) =>
        {
            await using var cmd = new NpgsqlCommand(
                """UPDATE "Ingredients" SET "Name" = 'hijacked' WHERE "Name" = 'shared gin';""", conn, tx);
            return await cmd.ExecuteNonQueryAsync();
        });

        Assert.Equal(0, updated);
        Assert.True(await SharedGinStillExistsAsync());
    }

    [Fact]
    public async Task Delete_RemovesTheHouseholdsOwnRow()
    {
        // The mirror of the two above: the policy is restrictive about the catalog, not about the
        // household's own data. Without this, all three could pass on a table nobody can write at all.
        var deleted = await AsHouseholdAsync(_mine, async (conn, tx) =>
        {
            await using var cmd = new NpgsqlCommand(
                """DELETE FROM "Ingredients" WHERE "Name" = 'house infusion';""", conn, tx);
            return await cmd.ExecuteNonQueryAsync();
        });

        Assert.Equal(1, deleted);
    }

    [Fact]
    public async Task Insert_WithNullTenant_IsRejected()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            AsHouseholdAsync(_mine, async (conn, tx) =>
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    INSERT INTO "Ingredients" ("Id", "TenantId", "Name", "CategoryId")
                    SELECT gen_random_uuid(), NULL, 'smuggled', "CategoryId" FROM "Ingredients" LIMIT 1;
                    """, conn, tx);
                return await cmd.ExecuteNonQueryAsync();
            }));

        // 42501 = insufficient_privilege, which is how Postgres reports a WITH CHECK violation.
        Assert.Equal("42501", error.SqlState);
    }

    [Fact]
    public async Task Insert_WithNullTenant_IsAllowedUnderBypass()
    {
        // The seeder's path (and the only one). Bypass is set deliberately per command by
        // RlsSessionInterceptor for sanctioned system work — it is not something a slice drifts into.
        await using var conn = new NpgsqlConnection(RuntimeCs);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var set = new NpgsqlCommand($"SET LOCAL {RlsDdl.BypassGuc} = 'on'", conn, tx))
            await set.ExecuteNonQueryAsync();

        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO "Ingredients" ("Id", "TenantId", "Name", "CategoryId")
            SELECT gen_random_uuid(), NULL, 'seeded vermouth', "CategoryId" FROM "Ingredients" LIMIT 1;
            """, conn, tx))
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());

        await tx.CommitAsync();
    }

    [Fact]
    public async Task TenantLessContext_CanWriteASharedRow_ThroughEf()
    {
        // The seeding path, end to end and through the real wiring rather than a hand-set GUC:
        // RlsSessionInterceptor turns bypass ON for a context with no ambient household, which is the
        // only thing that gets an INSERT past rls_shared_or_tenant_insert. CatalogSeeder's refusal to
        // run under a household is the other half of this; together they are why "seed the catalog" and
        // "run as a system context" are the same statement (JJ-031).
        var lookups = await LookupIdsAsync();

        await using (var system = RuntimeContext(tenantId: null))
        {
            system.Ingredients.Add(new Ingredient
            {
                Name = "seeded orgeat",
                CategoryId = lookups,
                TenantId = null,
            });
            await system.SaveChangesAsync();
        }

        // Visible to a household afterwards, which is the entire point of a shared row.
        await using var household = RuntimeContext(_mine);
        Assert.Contains("seeded orgeat", await household.Ingredients.Select(i => i.Name).ToListAsync());
    }

    [Fact]
    public async Task HouseholdContext_CannotWriteASharedRow_ThroughEf()
    {
        // The mirror. Without this, the test above would pass on a database where anyone can write a
        // shared row, which is the failure the four-policy split exists to prevent.
        var lookups = await LookupIdsAsync();

        await using var household = RuntimeContext(_mine);
        household.Ingredients.Add(new Ingredient
        {
            Name = "smuggled orgeat",
            CategoryId = lookups,
            TenantId = null,
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => household.SaveChangesAsync());
        Assert.Equal("42501", (error.InnerException as PostgresException)?.SqlState);
    }

    /// <summary>A context on the RLS-subject runtime role, acting as the given household (null = system).</summary>
    private TestAppDbContext RuntimeContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>().UseNpgsql(RuntimeCs).Options;
        return new TestAppDbContext(options, new TestCurrentTenant { TenantId = tenantId });
    }

    /// <summary>The seeded category id, read back as the superuser.</summary>
    private async Task<Guid> LookupIdsAsync()
    {
        await using var db = fixture.CreateTestContext();
        return (await db.IngredientCategories.FirstAsync()).Id;
    }

    /// <summary>Runs <paramref name="work"/> as the runtime role with the tenant GUC set, in one transaction.</summary>
    private async Task<T> AsHouseholdAsync<T>(Guid tenantId, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work)
    {
        await using var conn = new NpgsqlConnection(RuntimeCs);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var set = new NpgsqlCommand($"SET LOCAL {RlsDdl.TenantGuc} = '{tenantId}'", conn, tx))
            await set.ExecuteNonQueryAsync();

        var result = await work(conn, tx);
        await tx.CommitAsync();
        return result;
    }

    /// <summary>Read back as the superuser, so the check itself is not subject to the policy under test.</summary>
    private async Task<bool> SharedGinStillExistsAsync()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(*) FROM "Ingredients" WHERE "Name" = 'shared gin';""", conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) == 1;
    }
}
