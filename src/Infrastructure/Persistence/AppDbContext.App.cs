using JiggerJot.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiggerJot.Infrastructure.Persistence;

/// <summary>
/// The app's half of the context (Arch A1, R159): its <c>DbSet</c>s, and any model rule of its own in
/// <c>OnAppModelCreating</c>, which the platform's <c>OnModelCreating</c> calls last. The other half
/// (<c>AppDbContext.cs</c>) is the platform's and stays identical across repos: the platform sets, both tenant
/// filters (the shared-or-tenant one moved upstream, Arch A4), the interceptors. JiggerJot's domain (JJ-031).
/// </summary>
public partial class AppDbContext
{
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
}
