namespace JiggerJot.Core.Entities;

/// <summary>
/// Where a seeded recipe came from — the book or list, and the credit owed to it (JJ-032).
/// <para>
/// This exists because a credit has to be a property of the row, not a promise in a footer. The
/// catalog mixes sources, so a flat "some of these are from somewhere" page cannot say which drink
/// came from where, and pulling one source later would mean re-deriving which rows to remove. With a
/// source id on the cocktail, both are a query.
/// </para>
/// <para>
/// A curated global lookup, like <see cref="GlassType"/> and the rest: no tenant column, no household
/// additions. A cocktail a household wrote itself has no source at all, which is the difference
/// between provenance from outside the app and <see cref="Cocktail.ForkedFromCocktailId"/>, which is
/// provenance from inside it.
/// </para>
/// </summary>
public class RecipeSource
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Short name, shown wherever a recipe is credited, e.g. "The Savoy Cocktail Book".</summary>
    public required string Name { get; set; }

    /// <summary>Year of the edition the recipes were taken from; null for a living list.</summary>
    public int? Year { get; set; }

    /// <summary>Where the material came from, so a reader can check it.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// The one-line credit and rights note for this source, written out rather than derived, because
    /// the sources are not on the same footing and a template would flatten that.
    /// </summary>
    public string? Attribution { get; set; }
}
