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

        // JJ-005 as a query filter rather than as prose. Both ends of a substitution must be SHARED
        // catalog ingredients — a household's own ingredient satisfies a recipe line by exact match
        // only (JJ-018) — and this says so where the model can enforce it.
        //
        // It also settles a warning EF was right to raise: Ingredient carries a query filter and is
        // the REQUIRED end of both relationships here, so without a matching filter on this side EF
        // cannot rule out a row whose required navigation filters away to null. That cannot actually
        // happen while the invariant above holds, but "cannot happen" is exactly the kind of claim
        // worth writing down where the compiler can keep it.
        s.HasQueryFilter(x => x.Ingredient!.TenantId == null && x.SubstituteIngredient!.TenantId == null);
    }
}
