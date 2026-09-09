using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Recipe lines. Dual-natured, following the parent cocktail (JJ-031).
/// </summary>
public class CocktailIngredientConfiguration : IEntityTypeConfiguration<CocktailIngredient>
{
    public void Configure(EntityTypeBuilder<CocktailIngredient> l)
    {
        l.HasKey(x => x.Id);
        l.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        l.Property(x => x.Notes).HasMaxLength(500);

        // Enough scale for a third of an ounce and for "1.5" alike; stored as authored (JJ-007).
        l.Property(x => x.Amount).HasPrecision(10, 4);

        // Cascade: lines have no meaning without their cocktail, and deleting a household's cocktail
        // must take its lines with it.
        l.HasOne(x => x.Cocktail).WithMany(x => x.Lines)
            .HasForeignKey(x => x.CocktailId).OnDelete(DeleteBehavior.Cascade);
        l.HasOne(x => x.Ingredient).WithMany()
            .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        l.HasOne(x => x.Unit).WithMany()
            .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);

        // Deliberately NOT unique on (CocktailId, IngredientId): one ingredient may legitimately
        // appear on several lines of a drink.
        l.HasIndex(x => x.CocktailId);
        l.HasIndex(x => x.IngredientId);
        l.HasIndex(x => x.TenantId);

        // The makeable engine reads required lines per cocktail; this is its access path.
        l.HasIndex(x => new { x.CocktailId, x.IsRequired });
    }
}
