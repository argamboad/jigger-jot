using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Curated global lookup (JJ-022) — no tenant column. The conversion factor is stored with enough
/// scale that round-tripping an imperial amount through millilitres and back doesn't drift
/// (JJ-007: amounts are stored as authored, so any drift would show up only at display time).
/// </summary>
public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> u)
    {
        u.HasKey(x => x.Id);
        u.Property(x => x.Name).HasMaxLength(50).IsRequired();
        u.Property(x => x.System).HasConversion<string>().HasMaxLength(20).IsRequired();
        u.Property(x => x.MillilitreFactor).HasPrecision(12, 6);
        u.HasIndex(x => x.Name).IsUnique();
    }
}
