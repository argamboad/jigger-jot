using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Global-only substitution graph (JJ-004, JJ-005) — no tenant column, so no filter and no policy.
/// Both directions are stored as separate rows (JJ-006).
/// </summary>
public class IngredientSubstitutionConfiguration : IEntityTypeConfiguration<IngredientSubstitution>
{
    public void Configure(EntityTypeBuilder<IngredientSubstitution> s)
    {
        s.HasKey(x => x.Id);

        s.HasOne(x => x.Ingredient).WithMany()
            .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);
        // Restrict on the second leg: two cascade paths into the same table is a multiple-cascade-path
        // error in Postgres, and one cascade is enough to keep the graph clean.
        s.HasOne(x => x.SubstituteIngredient).WithMany()
            .HasForeignKey(x => x.SubstituteIngredientId).OnDelete(DeleteBehavior.Restrict);

        // The makeable query walks this by IngredientId, looking for what could stand in.
        s.HasIndex(x => x.IngredientId);
        s.HasIndex(x => new { x.IngredientId, x.SubstituteIngredientId }).IsUnique();
    }
}
