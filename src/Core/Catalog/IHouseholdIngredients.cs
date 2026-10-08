namespace JiggerJot.Core.Catalog;

/// <summary>
/// The Inventory slice's face for a household's own ingredients (Arch A8, R162 — one slice writes an entity; others
/// read it or go through a Core contract the owner implements). The shelf creates and removes a household's custom
/// bottles; the Catalog's dissolve, which must delete the household's recipe lines and cocktails first (they reference
/// the bottles), wipes the bottles through here. The shared catalog is never touched.
/// </summary>
public interface IHouseholdIngredients
{
    /// <summary>Household dissolution: every ingredient <paramref name="tenantId"/> owns goes. Set-based, immediate.</summary>
    Task WipeAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
