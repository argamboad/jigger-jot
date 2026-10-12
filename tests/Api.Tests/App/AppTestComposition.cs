using JiggerJot.Api.Features.Catalog;
using JiggerJot.Api.Features.Inventory;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace JiggerJot.Api.Tests.App;

/// <summary>
/// The app's half of the test chassis (Arch A1, R159). <c>ServiceHarness</c> and <c>IntegrationTestFactory</c> are
/// the platform's and stay identical across repos; what an app adds to them is said here: the contributors its
/// slices register, so an accept-and-dissolve test consults what production consults (an empty household must read
/// as empty to every slice, and every slice's wipe runs inside the dissolve); and the pins its integration host
/// needs regardless of the developer's <c>.env</c>. JiggerJot needs no pins.
/// </summary>
internal static class AppTestComposition
{
    /// <summary>JiggerJot's <see cref="ITenantDataContributor"/>s as production resolves them: the household's catalog and its shelf.</summary>
    public static IEnumerable<ITenantDataContributor> Contributors(AppDbContext db, TimeProvider clock) =>
    [
        new CatalogDataContributor(new EfRepository<Ingredient>(db), new EfRepository<Cocktail>(db), new EfRepository<CocktailIngredient>(db),
            new HouseholdIngredients(new EfRepository<Ingredient>(db))),
        new InventoryDataContributor(new EfRepository<TenantInventory>(db)),
    ];

    /// <summary>Process-level pins the host reads at <c>CreateBuilder</c> time (environment variables); runs before the host is built.</summary>
    public static void PinEnvironment()
    {
    }

    /// <summary>Service swaps for the integration host, after the platform's own (the throwaway database, the test auth handler).</summary>
    public static void ConfigureTestServices(IServiceCollection services)
    {
    }
}
