using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Curated global lookup (JJ-022) — no tenant column. The exact conversion factor is kept with full
/// scale, though no recipe reads it: amounts are stored in ounces and read at the bar's 30 ml ounce
/// by <c>BarMeasure</c> (JJ-041).
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
