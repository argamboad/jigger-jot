using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// Minimal fixtures for the dual-natured catalog tables. Every one of them hangs off curated global
/// lookups (JJ-022), so a test that wants a single cocktail still needs a category, a glass, a method
/// and a unit — this puts that boilerplate in one place instead of five tests.
/// <para>
/// Writes go through a context with <b>no ambient tenant</b> and set <c>TenantId</c> explicitly, because
/// nothing stamps an <see cref="ISharedOrTenantScoped"/> row (JJ-031, consequence 1). That is exactly how
/// the seeder and the fork path will have to behave, so the tests exercise the real shape.
/// </para>
/// </summary>
public sealed record CatalogLookups(Guid CategoryId, Guid GlassTypeId, Guid MethodId, Guid UnitId);

public static class CatalogSeed
{
    /// <summary>Creates the four global lookups every catalog row needs.</summary>
    public static async Task<CatalogLookups> LookupsAsync(AppDbContext db)
    {
        var category = new IngredientCategory { Name = "Gin" };
        var glass = new GlassType { Name = "Coupe" };
        var method = new Method { Name = "Shake" };
        var unit = new Unit { Name = "oz", System = UnitSystem.Imperial, MillilitreFactor = 29.5735m };
        db.AddRange(category, glass, method, unit);
        await db.SaveChangesAsync();
        return new CatalogLookups(category.Id, glass.Id, method.Id, unit.Id);
    }

    /// <summary>An ingredient owned by <paramref name="tenantId"/>, or shared when it is null.</summary>
    public static async Task<Ingredient> IngredientAsync(
        AppDbContext db, CatalogLookups lookups, string name, Guid? tenantId)
    {
        var ingredient = new Ingredient { Name = name, CategoryId = lookups.CategoryId, TenantId = tenantId };
        db.Add(ingredient);
        await db.SaveChangesAsync();
        return ingredient;
    }

    /// <summary>
    /// A cocktail with one required line, owned by <paramref name="tenantId"/> or shared when it is null.
    /// The line carries the parent's nature, which is the invariant the whole shape depends on.
    /// </summary>
    public static async Task<Cocktail> CocktailAsync(
        AppDbContext db, CatalogLookups lookups, string name, Guid? tenantId, Guid ingredientId)
    {
        var cocktail = new Cocktail
        {
            Name = name,
            TenantId = tenantId,
            GlassTypeId = lookups.GlassTypeId,
            MethodId = lookups.MethodId,
            ServingType = ServingType.FullDrink,
            Lines =
            [
                new CocktailIngredient
                {
                    TenantId = tenantId,
                    IngredientId = ingredientId,
                    Amount = 2m,
                    UnitId = lookups.UnitId,
                    Role = RecipeRole.Base,
                },
            ],
        };
        db.Add(cocktail);
        await db.SaveChangesAsync();
        return cocktail;
    }
}
