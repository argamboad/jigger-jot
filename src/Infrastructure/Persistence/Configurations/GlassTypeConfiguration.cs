using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>Curated global lookup (JJ-022) — no tenant column.</summary>
public class GlassTypeConfiguration : IEntityTypeConfiguration<GlassType>
{
    public void Configure(EntityTypeBuilder<GlassType> g)
    {
        g.HasKey(x => x.Id);
        g.Property(x => x.Name).HasMaxLength(100).IsRequired();
        g.HasIndex(x => x.Name).IsUnique();
    }
}
