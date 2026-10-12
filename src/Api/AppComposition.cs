using JiggerJot.Api.Features.Catalog;
using JiggerJot.Api.Features.Inventory;
using JiggerJot.Api.Features.UnitPreference;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Catalog;
using JiggerJot.Infrastructure.Persistence.Seed;

namespace JiggerJot.Api;

/// <summary>
/// The app's half of composition (Arch A1, R159): the one file outside <c>src/Api/Features/</c> that may name a
/// slice (R8, as amended). <c>Program.cs</c> calls <see cref="AddAppServices"/> once after the platform's
/// registrations and <see cref="MapAppEndpoints"/> once after the platform's routes, and is otherwise identical in
/// the platform and every app. A slice registers its handler, its <see cref="ITenantDataContributor"/> and any
/// <see cref="IStartupTask"/> here and maps its group here; nothing else central is edited (the add-a-slice
/// checklist in <c>docs/WAYS_OF_WORKING.md</c>). JiggerJot's domain (epic CKTL): the catalog and the shelf.
/// </summary>
public static class AppComposition
{
    /// <summary>Registers every slice's services. Behind each slice's own <c>Enabled</c> setting where it has one.</summary>
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        // The household's own catalog. Its contributor is load-bearing beyond dissolve: the catalog rows are
        // shared-or-tenant (ISharedOrTenantScoped), so the household's own rows go only through it (JJ-031).
        services.AddScoped<ITenantDataContributor, CatalogDataContributor>();
        services.AddScoped<CocktailBrowseHandler>();
        services.AddScoped<CocktailDetailHandler>();
        services.AddScoped<CocktailForkHandler>();
        services.AddScoped<CocktailAuthoringHandler>();
        // The curated global lookups (SEED-1, JJ-022), seeded at startup on a scope with no ambient household —
        // the only context the RLS insert policy lets write a shared row (JJ-031).
        services.AddScoped<CatalogSeeder>();
        services.AddScoped<IStartupTask, CatalogSeedStartupTask>();

        // The household's shelf.
        services.AddScoped<InventoryHandler>();
        services.AddScoped<IngredientRemovalHandler>();
        services.AddScoped<ITenantDataContributor, InventoryDataContributor>();
        services.AddScoped<IHouseholdIngredients, HouseholdIngredients>();     // the Catalog's dissolve wipes bottles through the shelf (Arch A8)

        // The reader's measuring system (JJ-008), user-keyed (Arch A3, jigger-jot#164).
        services.AddScoped<UnitPreferenceHandler>();
        services.AddScoped<IUserDataContributor, UnitPreferenceUserDataContributor>();
        return services;
    }

    /// <summary>Maps every slice's endpoint group (each through <c>MapTenantFeatureGroup</c>, R6).</summary>
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCocktails();
        app.MapInventory();
        app.MapUnitPreference();
        return app;
    }
}
