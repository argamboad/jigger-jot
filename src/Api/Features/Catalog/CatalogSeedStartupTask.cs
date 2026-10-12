using JiggerJot.Core.Abstractions;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// The curated global lookups (SEED-1, JJ-022): glass types, methods, units and ingredient categories. A startup task
/// (Arch A1): the platform runs it after <c>Migrate()</c>, because it needs the tables, and before anything serves,
/// because the catalog vocabulary is what every recipe row points at. Idempotent — a boot with nothing to add is a
/// count query per lookup and no writes. The scope carries no ambient household, which is both what the RLS insert
/// policy requires for a shared row and what <see cref="CatalogSeeder"/> asserts before it writes (JJ-031).
/// </summary>
public sealed class CatalogSeedStartupTask(IConfiguration configuration, AppDbContext db, CatalogSeeder seeder) : IStartupTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue(CatalogSeeder.EnabledConfigKey, true)) return;
        if (!db.Database.IsRelational()) return;
        await seeder.SeedAsync();
    }
}
