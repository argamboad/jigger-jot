using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>Curated global lookup (JJ-015, JJ-022) — no tenant column, so no tenant filter or RLS policy.</summary>
public class IngredientCategoryConfiguration : IEntityTypeConfiguration<IngredientCategory>
{
    public void Configure(EntityTypeBuilder<IngredientCategory> c)
    {
        c.HasKey(x => x.Id);
        c.Property(x => x.Name).HasMaxLength(100).IsRequired();

        // Self-referencing, deliberately two levels deep (JJ-015). Restrict: a category with
        // subcategories must be emptied before it can go, rather than silently taking them along.
        c.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        c.HasIndex(x => x.ParentId);
        c.HasIndex(x => new { x.ParentId, x.Name }).IsUnique();
    }
}
