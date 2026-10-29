using Microsoft.EntityFrameworkCore;
using Perezosoft.Core.Abstractions;
using JiggerJot.Host.Entities;
using Perezosoft.Core.Entities;
using Perezosoft.Infrastructure.Persistence;

namespace JiggerJot.Host.Persistence;

/// <summary>
/// JiggerJot's database context: the platform's half is <see cref="PlatformDbContext"/> (its sets, the tenant filter, the
/// interceptors), in the Infrastructure package; this is the app's half — its sets here, its model rules in
/// <see cref="ApplyAppModel"/>, its entity configurations discovered from this assembly. Its migrations live beside it.
/// </summary>
public class AppDbContext : PlatformDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
        : this((DbContextOptions)options, currentTenant) { }

    /// <summary>For a test harness that subclasses this context with its own options (a test-only fixture entity).</summary>
    protected AppDbContext(DbContextOptions options, ICurrentTenant currentTenant)
        : base(options, currentTenant) { }

    // Curated global lookups: no tenant column at all, so no filter and no RLS policy (JJ-022).
    public DbSet<IngredientCategory> IngredientCategories => Set<IngredientCategory>();
    public DbSet<GlassType> GlassTypes => Set<GlassType>();
    public DbSet<Method> Methods => Set<Method>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<RecipeSource> RecipeSources => Set<RecipeSource>();

    // Global-only substitution graph, both directions stored (JJ-005, JJ-006).
    public DbSet<IngredientSubstitution> IngredientSubstitutions => Set<IngredientSubstitution>();

    // Shared-or-tenant: the shared catalog (TenantId null) and household-owned rows in one table (ISharedOrTenantScoped,
    // Arch A4). The platform filters them and RlsDdl writes their four policies.
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Cocktail> Cocktails => Set<Cocktail>();
    public DbSet<CocktailIngredient> CocktailIngredients => Set<CocktailIngredient>();

    // The household's shelf — ordinary ITenantScoped data, fully covered by the platform.
    public DbSet<TenantInventory> TenantInventories => Set<TenantInventory>();

    // The reader's measuring system, user-keyed (Arch A3, jigger-jot#164).
    public DbSet<UserUnitPreference> UserUnitPreferences => Set<UserUnitPreference>();

    // The app's entity configurations: this assembly's (a test subclass in another assembly still gets this one's).
    protected override IEnumerable<System.Reflection.Assembly> AppModelAssemblies => [typeof(AppDbContext).Assembly];

    /// <summary>The app's own model rules, applied after the platform's.</summary>
    protected override void ApplyAppModel(ModelBuilder builder) { }
}
