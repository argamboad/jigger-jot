namespace JiggerJot.Shared.Ui.Components;

/// <summary>One bottle the write form's <see cref="IngredientPicker"/> can offer.</summary>
/// <param name="Category">Its top-level category, shown beside the name and matched when typing.</param>
/// <param name="OnShelf">Ticked on this household's shelf, so the picker can say so.</param>
public sealed record IngredientOption(Guid Id, string Name, string Category, bool OnShelf);
