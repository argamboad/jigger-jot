namespace JiggerJot.Core.Entities;

/// <summary>
/// Two-level ingredient categorization (JJ-015): a self-referencing tree kept deliberately at two
/// levels — a top-level category (<c>ParentId</c> null, e.g. "Rum") and its subcategories
/// (e.g. "Dark Rum"). Filtering on a parent matches every child, so "all rum" is one query
/// (JJ-016). A curated global lookup: no household additions in MVP (JJ-022), so no tenant column.
/// </summary>
public class IngredientCategory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    /// <summary>Null = a top-level category; set = a subcategory of that category.</summary>
    public Guid? ParentId { get; set; }

    public IngredientCategory? Parent { get; set; }
    public ICollection<IngredientCategory> Children { get; set; } = [];
}
