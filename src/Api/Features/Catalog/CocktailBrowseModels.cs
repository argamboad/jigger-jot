namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// What a browse request may ask for (CKTL-2). Request/response DTOs live with the feature, not in a
/// shared models project.
/// </summary>
/// <param name="Search">Matches anywhere in the name, case-insensitively. Null or blank browses everything.</param>
/// <param name="Page">1-based. Anything below 1 is clamped rather than rejected — a bad page number is
/// a caller's typo, not a reason to hand back a 400 for a read.</param>
/// <param name="PageSize">Clamped to <see cref="MaxPageSize"/>. The endpoint supplies
/// <see cref="DefaultPageSize"/> when the caller omits it, so there is no second default here to
/// drift from that one. The cap is the endpoint's only protection against a single request walking
/// the whole catalog, and the catalog is nearly a thousand recipes.</param>
/// <param name="MakeableOnly">FEATURES §11 lists "makeable now" as an on/off filter alongside
/// ingredient, method, glass and serving type — combinable, on one screen — rather than as a
/// separate view. It is a filter here for the same reason.</param>
/// <param name="AlmostMakeableOnly">Exactly one required line unsatisfied, after substitutions
/// (FEATURES §10, JJ-019). "Adjacent to what you can make" is one more filter on the same list, not a
/// second screen, for the same reason as above. Setting this <i>and</i>
/// <paramref name="MakeableOnly"/> asks for drinks that are both zero short and one short: the
/// handler applies both predicates and the page comes back empty, rather than one flag quietly
/// winning over the other.</param>
public record CocktailBrowseRequest(
    string? Search,
    int Page,
    int PageSize,
    bool MakeableOnly = false,
    bool AlmostMakeableOnly = false)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int SafePage => Page < 1 ? 1 : Page;

    public int SafePageSize => PageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => PageSize,
    };

    public string? SafeSearch => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

/// <summary>One page of results, with the total so a caller can show how far the list runs.</summary>
public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

/// <summary>
/// A cocktail as a browse row. Deliberately not the whole recipe: a list of a thousand drinks should
/// not carry three and a half thousand ingredient lines with it, so the lines are counted here and
/// read in full on the detail screen (CKTL-3).
/// </summary>
/// <param name="Glass">Null when the recipe never said (JJ-034), which the UI renders as nothing at all.</param>
/// <param name="Method">Null for the same reason.</param>
/// <param name="Source">The book or list this came from; null for a cocktail the household wrote.
/// Load-bearing in a list, not decoration — four names appear in both books and one appears twice in
/// the Savoy alone, so without it the browse shows duplicate rows and no way to tell them apart.</param>
/// <param name="IsOwn">True when this row belongs to the household rather than the shared catalog.</param>
/// <param name="Substitutions">Why this drink qualified when the household does not have exactly
/// what the recipe asks for (FEATURES §9). Empty unless a makeability filter is on, because outside
/// one a row makes no claim about being makeable and a stray "using X instead of Y" would imply one.</param>
/// <param name="MissingIngredient">The one bottle standing between this household and this drink
/// (FEATURES §10, JJ-019) — the whole point of the almost-makeable list, since "you cannot make this"
/// on its own is not a shopping list. Null unless the almost-makeable filter is on: a makeable row is
/// missing nothing, and an unfiltered row was never measured.</param>
public record CocktailSummary(
    Guid Id,
    string Name,
    string? Glass,
    string? Method,
    string ServingType,
    string? Source,
    bool IsOwn,
    int IngredientCount,
    IReadOnlyList<SubstitutionInPlay> Substitutions,
    string? MissingIngredient = null);

/// <summary>
/// "Using Kahlúa in place of Tia Maria." A result shown thanks to a substitution has to say so, or
/// the household is told it can make something and finds the bottle missing when it reaches the shelf
/// (FEATURES §9).
/// </summary>
/// <param name="AsksFor">What the recipe calls for.</param>
/// <param name="YouHave">What the household actually has, and would pour.</param>
public record SubstitutionInPlay(string AsksFor, string YouHave);

/// <summary>
/// One cocktail, whole (CKTL-3). Everything the detail screen shows and nothing it does not.
/// </summary>
/// <param name="Source">The book or list, with the credit owed to it (JJ-032). Null for a cocktail
/// the household wrote.</param>
public record CocktailDetail(
    Guid Id,
    string Name,
    string? Glass,
    string? Method,
    string ServingType,
    string? Instructions,
    CocktailSourceView? Source,
    bool IsOwn,
    IReadOnlyList<RecipeLineView> Lines);

/// <param name="Attribution">Written out per source, because the sources are not on the same footing
/// and a template would flatten that.</param>
public record CocktailSourceView(string Name, int? Year, string? Url, string? Attribution);

/// <summary>
/// A recipe line as the reader sees it. Both forms are here on purpose: <paramref name="Amount"/> and
/// <paramref name="Unit"/> are what the author wrote and never change, while
/// <paramref name="Display"/> is that same amount rendered into the reader's preferred system
/// (JJ-007, JJ-008). A client that wants to do its own formatting still can.
/// </summary>
/// <param name="IsRequired">False for a garnish, and optional lines never block makeability (JJ-009).</param>
public record RecipeLineView(
    string Ingredient,
    decimal? Amount,
    string? Unit,
    string Display,
    bool IsRequired,
    string Role,
    string? Notes);
