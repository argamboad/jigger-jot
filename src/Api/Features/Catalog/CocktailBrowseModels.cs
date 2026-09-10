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
public record CocktailBrowseRequest(string? Search, int Page, int PageSize)
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
public record CocktailSummary(
    Guid Id,
    string Name,
    string? Glass,
    string? Method,
    string ServingType,
    string? Source,
    bool IsOwn,
    int IngredientCount);

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
