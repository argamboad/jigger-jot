namespace JiggerJot.Core.Entities;

/// <summary>
/// Marks an entity whose rows are EITHER shared catalog data (<c>TenantId</c> null, readable by every
/// household and owned by none) OR household-owned (<c>TenantId</c> set) — the JiggerJot catalog shape
/// (JJ-011, JJ-012, decided in JJ-031).
/// <para>
/// This is the deliberate sibling of <see cref="ITenantScoped"/>, not a way around it.
/// <see cref="ITenantScoped"/> cannot express these rows at all: its <c>TenantId</c> is non-nullable, and
/// both the global query filter and the forced RLS policy test <c>TenantId = current</c>, so a shared row
/// would be invisible in the app AND at the database. Entities marked here get a parallel filter in
/// <c>AppDbContext.OnModelCreating</c> — <c>TenantId == null || TenantId == CurrentTenantId</c> — mirrored
/// by a hand-written RLS policy shipped in the same migration that creates the table.
/// </para>
/// <para>
/// <b>Three platform guarantees do NOT apply to entities marked with this interface</b>, because every one
/// of them keys off <see cref="ITenantScoped"/> or a non-nullable <c>TenantId</c>. Each is replaced by an
/// app-level test or an explicit call site — see JJ-031:
/// <list type="number">
/// <item><c>TenantStampingInterceptor</c> does not stamp them: set <c>TenantId</c> explicitly when creating
/// a household-owned row.</item>
/// <item><c>RlsMigrationGateTests</c> does not check their policy: an app test asserts it survives migration.</item>
/// <item><c>EveryTenantOwnedEntity_IsWiredIntoTenantDissolution</c> does not flag them: their
/// <c>ITenantDataContributor</c> must wipe the household's own rows and never the shared catalog.</item>
/// </list>
/// </para>
/// </summary>
public interface ISharedOrTenantScoped
{
    /// <summary>Null = shared catalog row; set = owned by that household.</summary>
    Guid? TenantId { get; }
}
