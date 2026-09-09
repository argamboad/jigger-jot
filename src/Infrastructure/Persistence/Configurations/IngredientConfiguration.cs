using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Dual-natured: shared catalog (TenantId null) and household-owned rows in one table (JJ-011,
/// JJ-031). Filtered by the ISharedOrTenantScoped loop in AppDbContext, backed by a hand-written
/// RLS policy in the creating migration.
/// </summary>
public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> i)
    {
        i.HasKey(x => x.Id);
        i.Property(x => x.Name).HasMaxLength(200).IsRequired();

        // Restrict, not cascade: a category that still classifies ingredients must be dealt with
        // deliberately. Cascading would silently delete catalog rows every household can see.
        i.HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        i.HasOne(x => x.Subcategory).WithMany()
            .HasForeignKey(x => x.SubcategoryId).OnDelete(DeleteBehavior.Restrict);

        // TenantId leads every index: the query filter always constrains on it, so a scan of one
        // household's rows never walks the whole shared catalog.
        i.HasIndex(x => x.TenantId);
        i.HasIndex(x => new { x.TenantId, x.CategoryId });

        // Unique per owner, case-insensitively: the catalog must not hold two "Dry Gin", and a
        // household must not shadow its own custom ingredient. Postgres treats NULLs as distinct by
        // default, so NULLS NOT DISTINCT is required for the shared rows (TenantId null) to be
        // covered at all — without it the uniqueness would silently apply to households only.
        i.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_Ingredients_TenantId_Name_Unique");
    }
}
