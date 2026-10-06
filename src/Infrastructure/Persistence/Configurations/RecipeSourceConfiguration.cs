using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Curated global lookup, no tenant column (JJ-022, JJ-032).
/// </summary>
public class RecipeSourceConfiguration : IEntityTypeConfiguration<RecipeSource>
{
    public void Configure(EntityTypeBuilder<RecipeSource> s)
    {
        s.HasKey(x => x.Id);
        s.Property(x => x.Name).HasMaxLength(200).IsRequired();
        s.Property(x => x.Url).HasMaxLength(500);
        s.Property(x => x.Attribution).HasMaxLength(1000);

        s.HasIndex(x => x.Name).IsUnique();
    }
}
