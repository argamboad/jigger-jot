using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>
/// The household's shelf. Ordinary ITenantScoped data (JJ-031), so the platform's global filter,
/// write stamping and generated RLS policy all cover it without anything hand-written.
/// </summary>
public class TenantInventoryConfiguration : IEntityTypeConfiguration<TenantInventory>
{
    public void Configure(EntityTypeBuilder<TenantInventory> t)
    {
        t.HasKey(x => x.Id);

        t.HasOne(x => x.Ingredient).WithMany()
            .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Cascade);

        t.HasIndex(x => x.TenantId);

        // Sparse and one-row-per-ingredient (JJ-023): absence means "not available", so a duplicate
        // row would make availability ambiguous.
        t.HasIndex(x => new { x.TenantId, x.IngredientId }).IsUnique();
    }
}
