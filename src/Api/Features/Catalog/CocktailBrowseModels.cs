using JiggerJot.Core.Entities;

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
/// <param name="Ingredient">"Everything with vodka" (FEATURES §11, FILTER-1). Matched against the
/// recipe lines rather than any stored classification — there is no "main spirit" field and there
/// never will be, because a drink with two spirits or none makes that field a lie (JJ-014). The text
/// is compared against the ingredient's <b>name, its category and its subcategory</b> at once, so a
/// parent catches every child: "rum" finds the dark and the white, "dark rum" finds only the dark,
/// and "elderflower" finds a thing no editor would have thought to tag (JJ-015, JJ-016).</param>
/// <param name="MethodId">Shake, stir, build. From <c>GET /api/cocktails/filters</c>.</param>
/// <param name="GlassTypeId">Likewise. A recipe that never stated a glass (JJ-034) is not swept into
/// whichever glass was asked for — it simply does not match.</param>
/// <param name="ServingType">Shot or full drink.</param>
public record CocktailBrowseRequest(
    string? Search,
    int Page,
    int PageSize,
    bool MakeableOnly = false,
    bool AlmostMakeableOnly = false,
    string? Ingredient = null,
    Guid? MethodId = null,
    Guid? GlassTypeId = null,
    ServingType? ServingType = null)
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

    /// <summary>Blank is no filter at all, not a filter for nothing.</summary>
    public string? SafeIngredient => string.IsNullOrWhiteSpace(Ingredient) ? null : Ingredient.Trim();
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
/// One bottle and the drinks it would open (ALMOST-2, JJ-035) — the almost-makeable set read the
/// other way round.
/// <para>
/// ALMOST-1 answers per drink: <i>this cocktail is missing that bottle</i>. Asked eighty-one times
/// that is a list nobody reads. This groups the same set by the missing ingredient, so one sentence
/// can say "buy this and four open up".
/// </para>
/// </summary>
/// <param name="Unlocks">Always equal to <paramref name="Cocktails"/>' length. The card shows the two
/// side by side, so they are one value read twice rather than two values computed twice — which is
/// also why the names are never truncated.</param>
public record UnlockingBottle(
    Guid IngredientId,
    string Ingredient,
    int Unlocks,
    IReadOnlyList<string> Cocktails);

/// <summary>One lookup value the catalog actually uses, for a filter dropdown.</summary>
public record FilterOption(Guid Id, string Name);

/// <summary>
/// What the filter dropdowns on the browse screen offer (FILTER-1, FEATURES §11). Drawn from the
/// cocktails this household can see rather than from the curated lookup tables: most of the nineteen
/// glasses and ten methods go unused by any given catalog, and a filter that returns nothing looks
/// broken. It also means the lists grow by themselves when the full catalog is switched on.
/// </summary>
public record CatalogFilterOptions(
    IReadOnlyList<FilterOption> Methods,
    IReadOnlyList<FilterOption> Glasses,
    IReadOnlyList<string> ServingTypes);

/// <summary>
/// One cocktail, whole (CKTL-3). Everything the detail screen shows and nothing it does not.
/// </summary>
/// <param name="Source">The book or list, with the credit owed to it (JJ-032). Null for a cocktail
/// the household wrote.</param>
/// <param name="Makeability">Where this drink stands against the household's shelf right now —
/// <c>Makeable</c>, <c>AlmostMakeable</c> or <c>NotMakeable</c> (FEATURES §12, CKTL-4). Derived at
/// query time and never stored (JJ-003, JJ-019). Sent as a name rather than a number so the wire
/// format survives anyone reordering the enum.</param>
public record CocktailDetail(
    Guid Id,
    string Name,
    string? Glass,
    string? Method,
    string ServingType,
    string? Instructions,
    CocktailSourceView? Source,
    bool IsOwn,
    IReadOnlyList<RecipeLineView> Lines,
    string Makeability,
    ForkOriginView? ForkedFrom = null);

/// <summary>
/// What this cocktail was copied from (FORK-1, JJ-013) — provenance only. Null for anything that was
/// not forked, and null again once the original is gone: the link is not a foreign key, so a deleted
/// original leaves the copy standing and simply stops it being able to say where it came from.
/// </summary>
public record ForkOriginView(Guid Id, string Name);

/// <summary>The new cocktail's id, so the caller can go straight to it. Shared by the fork and
/// the authoring endpoints, which differ in how the drink came to exist and in nothing else.</summary>
public record CocktailCreatedResponse(Guid Id);

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
/// <param name="Availability">Whether the household can pour this line — <c>Have</c>,
/// <c>Substitute</c> or <c>Missing</c> (FEATURES §12). Per line rather than only per drink, because a
/// reader looking at a recipe wants to know <i>which</i> line to do something about, not merely that
/// one of them needs attention.</param>
/// <param name="SubstituteWith">The bottle actually reached for, set only when
/// <paramref name="Availability"/> is <c>Substitute</c> — "any substitution in play", named.</param>
public record RecipeLineView(
    string Ingredient,
    decimal? Amount,
    string? Unit,
    string Display,
    bool IsRequired,
    string Role,
    string? Notes,
    string Availability,
    string? SubstituteWith = null);
