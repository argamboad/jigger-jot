using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>Curated global lookup (JJ-022) — no tenant column.</summary>
public class MethodConfiguration : IEntityTypeConfiguration<Method>
{
    public void Configure(EntityTypeBuilder<Method> m)
    {
        m.HasKey(x => x.Id);
        m.Property(x => x.Name).HasMaxLength(100).IsRequired();
        m.HasIndex(x => x.Name).IsUnique();
    }
}
