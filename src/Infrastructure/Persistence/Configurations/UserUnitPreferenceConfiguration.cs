using JiggerJot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiggerJot.Infrastructure.Persistence.Configurations;

/// <summary>User-keyed (no TenantId, no RLS policy); one row per user, gone with the user (Arch A3, jigger-jot#164).</summary>
public class UserUnitPreferenceConfiguration : IEntityTypeConfiguration<UserUnitPreference>
{
    public void Configure(EntityTypeBuilder<UserUnitPreference> c)
    {
        c.HasKey(x => x.Id);
        c.HasIndex(x => x.UserId).IsUnique();
        c.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
