using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Dual-natured, like Ingredient (JJ-012, JJ-031). See IngredientConfiguration for the index rationale.
/// </summary>
public class CocktailConfiguration : IEntityTypeConfiguration<Cocktail>
{
    public void Configure(EntityTypeBuilder<Cocktail> c)
    {
        c.HasKey(x => x.Id);
        c.Property(x => x.Name).HasMaxLength(200).IsRequired();
        c.Property(x => x.ServingType).HasConversion<string>().HasMaxLength(20).IsRequired();

        c.HasOne(x => x.GlassType).WithMany()
            .HasForeignKey(x => x.GlassTypeId).OnDelete(DeleteBehavior.Restrict);
        c.HasOne(x => x.Method).WithMany()
            .HasForeignKey(x => x.MethodId).OnDelete(DeleteBehavior.Restrict);

        // ForkedFromCocktailId is provenance ONLY (JJ-013) — deliberately NOT a foreign key. A fork is
        // a snapshot copy, so it must survive the original being deleted; an FK would either block that
        // delete or cascade the household's own cocktail away with it.
        c.HasIndex(x => x.ForkedFromCocktailId);

        c.HasIndex(x => x.TenantId);
        c.HasIndex(x => new { x.TenantId, x.Name });
    }
}
