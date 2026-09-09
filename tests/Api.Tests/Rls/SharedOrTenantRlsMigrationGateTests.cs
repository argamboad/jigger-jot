using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Infrastructure.Persistence;

namespace JiggerJot.Api.Tests.Rls;

/// <summary>
/// JJ-031, consequence 2: the platform's <see cref="RlsMigrationGateTests"/> keys off
/// <c>ITenantScoped</c>, so it never looks at a dual-natured table — adding one without shipping its
/// policy would sail through CI with row-level security simply off. This is the replacement gate, and it
/// runs against the database built by the REAL migrations, not the model.
/// <para>
/// It also asserts all FOUR command-scoped policies, not merely "some policy exists": collapsing them
/// back into one <c>FOR ALL</c> policy would leave every household able to delete the shared catalog,
/// and a name-agnostic check would not notice.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SharedOrTenantRlsMigrationGateTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task EverySharedOrTenantTable_HasForcedRlsAndAllFourPolicies_AfterMigrations()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;
        var tables = RlsDdl.SharedOrTenantTables(model);
        Assert.NotEmpty(tables);

        await using var conn = new NpgsqlConnection(factory.DatabaseConnectionString);
        await conn.OpenAsync();

        var failures = new List<string>();
        foreach (var (table, _) in tables)
        {
            await using var cmd = new NpgsqlCommand(
                """
                SELECT c.relrowsecurity,
                       c.relforcerowsecurity,
                       COALESCE(ARRAY(SELECT p.policyname::text FROM pg_policies p
                                      WHERE p.schemaname = 'public' AND p.tablename = c.relname), '{}')
                FROM pg_class c
                WHERE c.relname = $1 AND c.relnamespace = 'public'::regnamespace;
                """, conn);
            cmd.Parameters.Add(new NpgsqlParameter { Value = table });

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                failures.Add($"{table}: table not found in migrated schema");
                continue;
            }
            if (!reader.GetBoolean(0)) failures.Add($"{table}: ROW LEVEL SECURITY not enabled");
            if (!reader.GetBoolean(1)) failures.Add($"{table}: ROW LEVEL SECURITY not FORCEd");

            var present = reader.GetFieldValue<string[]>(2);
            foreach (var expected in RlsDdl.SharedOrTenantPolicyNames.Where(n => !present.Contains(n)))
                failures.Add($"{table}: policy '{expected}' missing");
        }

        Assert.True(failures.Count == 0,
            "The shared-or-tenant RLS backstop (JJ-031) is incomplete on the migrated schema — every "
            + "ISharedOrTenantScoped table needs ENABLE + FORCE ROW LEVEL SECURITY and all four "
            + "command-scoped policies (add a migration using RlsDdl.SharedOrTenantStatementsFor):\n - "
            + string.Join("\n - ", failures));
    }
}
