using System.Text.Json.Serialization;
using JiggerJot.Core.Entities;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// A cocktail a household is writing from scratch (AUTHORING-1, FEATURES §14).
/// </summary>
/// <param name="GlassTypeId">Optional, and meant to stay that way (JJ-034): a quarter of the seeded
/// catalog never states a glass, and someone writing down what they actually pour should not have to
/// invent one.</param>
/// <param name="MethodId">Optional for the same reason.</param>
/// <param name="ServingType">By NAME — "Shot" or "FullDrink" — because that is what
/// <c>GET /api/cocktails/lookups</c> hands out and what every response has always sent. Numbers are
/// accepted too. The converter is on the property rather than configured globally: every other
/// endpoint in this API turns its enums into strings on the way out by hand, and quietly changing how
/// the whole surface serialises would be a contract change nobody asked for.</param>
/// <param name="Lines">In the order they should read. Display positions are the array's, not
/// something the caller numbers — a form that made someone type them is a form nobody finishes.</param>
public record AuthorCocktailRequest(
    string Name,
    Guid? GlassTypeId,
    Guid? MethodId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ServingType>))] ServingType ServingType,
    string? Instructions,
    IReadOnlyList<AuthorLineRequest> Lines);

/// <summary>
/// One line of that recipe.
/// </summary>
/// <param name="Amount">Stored exactly as written and converted only at display (JJ-007). Null is
/// allowed and means the recipe does not measure it.</param>
/// <param name="UnitId">Null for a line whose amount needs no unit — "1 egg". A unit without an
/// amount is refused, because the display formatter renders an amountless line as nothing at all and
/// it would be a line its author could never see.</param>
/// <param name="IsRequired">False for a garnish. Optional lines never block makeability (JJ-009).</param>
/// <param name="Role">Display grouping only; it never affects makeability (JJ-010). By name, like
/// the serving type above.</param>
public record AuthorLineRequest(
    Guid IngredientId,
    decimal? Amount,
    Guid? UnitId,
    bool IsRequired,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RecipeRole>))] RecipeRole Role,
    string? Notes);

/// <summary>Why the write did or did not happen. Named outcomes rather than exceptions: every one of
/// these is an ordinary thing for someone to get wrong in a form.</summary>
public enum AuthorCocktailOutcome
{
    Created,

    /// <summary>Blank, whitespace, or longer than the column.</summary>
    InvalidName,

    /// <summary>No recipe lines at all.</summary>
    NoLines,

    /// <summary>A line names an ingredient this household cannot see.</summary>
    UnknownIngredient,

    /// <summary>A line has an amount that is not one, a unit that does not exist, or a unit with no
    /// amount.</summary>
    InvalidLine,

    /// <summary>The glass or the method does not exist.</summary>
    UnknownLookup,
}

/// <param name="Id">The new cocktail, on <see cref="AuthorCocktailOutcome.Created"/> only.</param>
public record AuthorCocktailResult(AuthorCocktailOutcome Outcome, Guid? Id = null);

/// <summary>
/// Everything a household may choose while writing a cocktail (AUTHORING-1).
/// <para>
/// <b>Not the same list as <c>GET /api/cocktails/filters</c>, on purpose.</b> That one is derived
/// from the catalog, so a filter never offers a glass that returns nothing. This one is the whole
/// curated lookup (JJ-022), because a household writing down what it actually pours must be able to
/// reach a glass no seeded recipe happens to use.
/// </para>
/// </summary>
public record AuthoringOptions(
    IReadOnlyList<FilterOption> Glasses,
    IReadOnlyList<FilterOption> Methods,
    IReadOnlyList<FilterOption> Units,
    IReadOnlyList<string> ServingTypes,
    IReadOnlyList<string> Roles);
