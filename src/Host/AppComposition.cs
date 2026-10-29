using JiggerJot.Host.Features.Catalog;
using JiggerJot.Host.Features.Inventory;
using JiggerJot.Host.Features.UnitPreference;
using Perezosoft.Core;
using Perezosoft.Core.Abstractions;
using JiggerJot.Host.Catalog;
using JiggerJot.Host.Email;
using Perezosoft.Infrastructure.Email;
using JiggerJot.Host.Persistence.Seed;

namespace JiggerJot.Host;

/// <summary>
/// The app's half of composition (Arch A1, R159): the one file outside <c>src/Host/Features/</c> that may name a
/// slice (R8, as amended). <c>Program.cs</c> calls <see cref="AddAppServices"/> once after the platform's
/// registrations and <see cref="MapAppEndpoints"/> once after the platform's routes, and is otherwise identical in
/// the platform and every app. A slice registers its handler, its <see cref="ITenantDataContributor"/> and any
/// <see cref="IStartupTask"/> here and maps its group here; nothing else central is edited (the add-a-slice
/// checklist in <c>docs/WAYS_OF_WORKING.md</c>). JiggerJot's domain (epic CKTL): the catalog and the shelf.
/// </summary>
public static class AppComposition
{
    /// <summary>The app's options for the platform: its brand, its plans, its own permissions.</summary>
    public static void ConfigurePlatform(PlatformOptions options)
    {
        // The name the platform shows beyond the mails: the MFA issuer, the OpenAPI title, the telemetry service.
        options.Brand.Name = "JiggerJot";
    }

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

        // The mails are JiggerJot's own design (the display serif, the lockup, Marga): its renderer in place of the
        // platform's, registered after it (perezosoft-platform#462).
        services.AddSingleton<IBrandedEmail, JiggerJotEmail>();

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
