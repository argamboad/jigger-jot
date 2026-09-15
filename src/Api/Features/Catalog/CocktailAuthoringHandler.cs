using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// A household writes a cocktail from scratch (AUTHORING-1, FEATURES §14), and edits one it owns
/// (AUTHORING-2).
/// <para>
/// The flow's last line — "immediately participates in makeable / filtering like any other cocktail"
/// — costs nothing to honour, and that is the design paying out rather than luck. Both are derived
/// from the recipe lines at query time (JJ-003, JJ-014), so a drink written a second ago is exactly
/// as visible to the engine as one seeded from a 1930 book. There is no index to rebuild and no tag
/// to remember to set — and the same holds for an edit.
/// </para>
/// <para>
/// Everything here validates against what THIS household can see. A recipe line is a reference, and a
/// reference to a row you cannot see is how one household learns another exists.
/// </para>
/// <para>
/// <b>Every volume is stored in ounces</b> (JJ-041), whatever the writer typed it in: the lines go
/// through <see cref="BarMeasure.ToStored"/> before they are written, the same as the seeded catalog.
/// </para>
/// </summary>
public class CocktailAuthoringHandler(
    IRepository<Cocktail> cocktails,
    IRepository<Ingredient> ingredients,
    IRepository<Unit> units,
    IRepository<GlassType> glasses,
    IRepository<Method> methods,
    IRepository<CocktailIngredient> recipeLines,
    IUserRepository users,
    ICurrentTenant tenant)
{
    private const int MaxNameLength = 200;
    private const int MaxNotesLength = 500;

    public async Task<AuthorCocktailResult> CreateAsync(
        AuthorCocktailRequest request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId)
            return new AuthorCocktailResult(AuthorCocktailOutcome.UnknownLookup);

        var prepared = await PrepareAsync(request, tenantId, cancellationToken);
        if (prepared.Refusal is { } refusal)
            return new AuthorCocktailResult(refusal);

        var cocktail = new Cocktail
        {
            // Set by hand on the cocktail AND every line: nothing stamps these tables (JJ-031), and a
            // row written without one would land in the shared catalog where every household on the
            // platform would see it.
            TenantId = tenantId,
            Name = prepared.Name,
            GlassTypeId = request.GlassTypeId,
            MethodId = request.MethodId,
            ServingType = request.ServingType,
            Instructions = Tidy(request.Instructions),

            // No SourceId and no ForkedFromCocktailId: this was written here, not transcribed from a
            // book (JJ-032) and not copied from another recipe (JJ-013).
            Lines = prepared.Lines,
        };

        await cocktails.AddAsync(cocktail, cancellationToken);
        await cocktails.SaveChangesAsync(cancellationToken);

        return new AuthorCocktailResult(AuthorCocktailOutcome.Created, cocktail.Id);
    }

    /// <summary>
    /// Replaces a household cocktail's fields and every one of its lines (AUTHORING-2) — one it wrote, or
    /// one it forked. Held to exactly the rules of <see cref="CreateAsync"/>, and a refused edit writes
    /// nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shared catalog is read-only</b> (JJ-002): a book's recipe is visible, so it is not a 404,
    /// but it is <see cref="AuthorCocktailOutcome.ReadOnly"/> — the way to change it is to fork it. Another
    /// household's cocktail is simply not found, since <c>Query()</c> never returns it (JJ-031).
    /// </para>
    /// <para>
    /// A fork keeps <c>ForkedFromCocktailId</c> — it is still based on what it was based on — and the
    /// original is untouched, because a fork is a snapshot (JJ-013). <b>Last save wins</b>: there is no
    /// lock or version check, deliberately, for a household editing its own recipe book.
    /// </para>
    /// </remarks>
    public async Task<AuthorCocktailResult> UpdateAsync(
        Guid id, AuthorCocktailRequest request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId)
            return new AuthorCocktailResult(AuthorCocktailOutcome.NotFound);

        var cocktail = await cocktails.Query()
            .Include(c => c.Lines)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cocktail is null) return new AuthorCocktailResult(AuthorCocktailOutcome.NotFound);
        if (cocktail.TenantId != tenantId) return new AuthorCocktailResult(AuthorCocktailOutcome.ReadOnly);

        var prepared = await PrepareAsync(request, tenantId, cancellationToken);
        if (prepared.Refusal is { } refusal)
            return new AuthorCocktailResult(refusal);

        cocktail.Name = prepared.Name;
        cocktail.GlassTypeId = request.GlassTypeId;
        cocktail.MethodId = request.MethodId;
        cocktail.ServingType = request.ServingType;
        cocktail.Instructions = Tidy(request.Instructions);

        // Every line replaced rather than matched up: the form sends the whole recipe in order, and
        // nothing references a line's id. The old rows are orphans of a required relationship, so EF
        // deletes them in the same save.
        cocktail.Lines.Clear();

        // The new lines are added through their own repository, NOT through the collection. Each already
        // carries its client-generated id, and EF reads a keyed entity reached through a tracked parent
        // as an existing row — it sends an UPDATE that matches nothing and throws a concurrency error.
        foreach (var line in prepared.Lines)
        {
            line.CocktailId = cocktail.Id;
            await recipeLines.AddAsync(line, cancellationToken);
        }

        await cocktails.SaveChangesAsync(cancellationToken);

        return new AuthorCocktailResult(AuthorCocktailOutcome.Updated, cocktail.Id);
    }

    /// <summary>
    /// A household cocktail shaped for the write form to edit (AUTHORING-2), with its volumes in the
    /// writer's own unit — the stored ounces as millilitres for a metric reader (JJ-041), so what the
    /// form shows is what that person would type.
    /// </summary>
    public async Task<CocktailDraftResult> DraftAsync(
        Guid id, Guid? userId, CancellationToken cancellationToken)
    {
        var row = await cocktails.Query()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.TenantId,
                c.Name,
                c.GlassTypeId,
                c.MethodId,
                c.ServingType,
                c.Instructions,
                Lines = c.Lines
                    .OrderBy(l => l.DisplayOrder)
                    .Select(l => new
                    {
                        l.IngredientId,
                        l.Amount,
                        l.UnitId,
                        Unit = l.Unit == null ? null : l.Unit.Name,
                        l.IsRequired,
                        l.Role,
                        l.Notes,
                    })
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null) return new CocktailDraftResult(CocktailDraftOutcome.NotFound);
        if (tenant.TenantId is not { } tenantId || row.TenantId != tenantId)
            return new CocktailDraftResult(CocktailDraftOutcome.ReadOnly);

        var preference = userId is { } reader
            ? (await users.GetByIdAsync(reader, cancellationToken))?.PreferredUnitSystem
            : null;
        var writerUnit = BarMeasure.VolumeUnitFor(preference);
        var writerUnitId = await units.Query()
            .Where(u => u.Name == writerUnit)
            .Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var lines = row.Lines.Select(l =>
        {
            var shown = BarMeasure.ForWriter(new BarMeasure.Line(l.Amount, l.Unit), preference);
            return new CocktailDraftLine(
                l.IngredientId,
                shown.Amount,
                shown.Unit == l.Unit ? l.UnitId : writerUnitId,
                l.IsRequired,
                l.Role.ToString(),
                l.Notes);
        }).ToList();

        return new CocktailDraftResult(CocktailDraftOutcome.Found, new CocktailDraft(
            row.Id, row.Name, row.GlassTypeId, row.MethodId, row.ServingType.ToString(), row.Instructions, lines));
    }

    /// <summary>
    /// Everything the form may offer: the whole curated lookups, not the subset the catalog happens
    /// to use (JJ-022). See <see cref="AuthoringOptions"/> for why that differs from the filters.
    /// </summary>
    /// <param name="userId">Whose form it is. The units are the one list that depends on the reader:
    /// their own volume unit first — ounces, or millilitres for a metric reader — then every unit that
    /// is not a volume. Never a part, a period glass or another volume unit, because every volume is
    /// stored in ounces and those would only be converted away on save (JJ-041).</param>
    public async Task<AuthoringOptions> OptionsAsync(Guid? userId, CancellationToken cancellationToken)
    {
        var preference = userId is { } id
            ? (await users.GetByIdAsync(id, cancellationToken))?.PreferredUnitSystem
            : null;
        var mine = BarMeasure.VolumeUnitFor(preference);

        // Units otherwise keep their curated order rather than being alphabetised: it runs from the
        // ones a person pours most to the ones they pour least, which is the order a picker wants.
        var curated = await units.Query()
            .Select(u => new FilterOption(u.Id, u.Name)).ToListAsync(cancellationToken);

        return new(
            await glasses.Query().OrderBy(g => g.Name)
                .Select(g => new FilterOption(g.Id, g.Name)).ToListAsync(cancellationToken),
            await methods.Query().OrderBy(m => m.Name)
                .Select(m => new FilterOption(m.Id, m.Name)).ToListAsync(cancellationToken),
            [.. curated
                .Where(u => BarMeasure.IsOfferedTo(preference, u.Name))
                .OrderBy(u => u.Name == mine ? 0 : 1)],
            [.. Enum.GetNames<ServingType>()],
            [.. Enum.GetNames<RecipeRole>()]);
    }

    /// <summary>How many lines one suggestion covers. Far past any recipe; it only bounds the query.</summary>
    public const int MaxRoleSuggestions = 50;

    /// <summary>
    /// A role for each ingredient, in the order asked (AUTHORING-3) — the same rule the seeded catalog
    /// was built with (<see cref="RecipeRoles"/>). The whole recipe is asked at once, because only the
    /// first spirit in it is the base.
    /// </summary>
    /// <remarks>
    /// Through <c>Query()</c>, so the categories read are the shared catalog's and this household's
    /// own (JJ-031). An ingredient it cannot see is <see cref="RecipeRole.Other"/>, the same as an
    /// unknown id — answering with its real category would tell one household what another keeps.
    /// </remarks>
    public async Task<IReadOnlyList<RoleSuggestion>> SuggestRolesAsync(
        IReadOnlyList<Guid> ingredientIds, CancellationToken cancellationToken)
    {
        var asked = ingredientIds.Take(MaxRoleSuggestions).ToList();
        var distinct = asked.Distinct().ToList();

        var categories = await ingredients.Query()
            .Where(i => distinct.Contains(i.Id))
            .Select(i => new { i.Id, Category = i.Category!.Name })
            .ToDictionaryAsync(i => i.Id, i => (string?)i.Category, cancellationToken);

        var roles = RecipeRoles.Suggest([.. asked.Select(id => categories.GetValueOrDefault(id))]);

        return [.. asked.Select((id, index) =>
            new RoleSuggestion(id, roles[index].ToString(), RecipeRoles.IsRequired(roles[index])))];
    }

    /// <summary>
    /// What writing and editing both check before anything is written, and the lines they would write:
    /// the name, at least one line, the glass and method, each line's shape, every ingredient and unit
    /// visible to this household, and every volume converted to ounces (JJ-041). One place, so an edit
    /// can never be held to a looser rule than a new cocktail.
    /// </summary>
    private async Task<Prepared> PrepareAsync(
        AuthorCocktailRequest request, Guid tenantId, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            return Prepared.Refused(AuthorCocktailOutcome.InvalidName);

        // A cocktail with no lines is not merely empty, it is misleading: makeability counts
        // UNSATISFIED required lines, so a drink with none is "makeable" by the letter of the rule
        // and would sit in the list of things you can pour tonight, made of nothing.
        if (request.Lines is not { Count: > 0 } lines)
            return Prepared.Refused(AuthorCocktailOutcome.NoLines);

        if (!await LookupsExistAsync(request.GlassTypeId, request.MethodId, cancellationToken))
            return Prepared.Refused(AuthorCocktailOutcome.UnknownLookup);

        if (LineShape(lines) is { } badShape)
            return Prepared.Refused(badShape);

        // Query(), so this is the shared catalog plus this household's own ingredients and nothing
        // else (JJ-031). The same ingredient may appear on several lines (FEATURES §14), so the
        // check is on the distinct set.
        var wanted = lines.Select(l => l.IngredientId).Distinct().ToList();
        var visible = await ingredients.Query()
            .Where(i => wanted.Contains(i.Id)).CountAsync(cancellationToken);
        if (visible != wanted.Count)
            return Prepared.Refused(AuthorCocktailOutcome.UnknownIngredient);

        var wantedUnits = lines.Where(l => l.UnitId is not null).Select(l => l.UnitId!.Value).Distinct().ToList();
        var unitNames = await units.Query()
            .Where(u => wantedUnits.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
        if (unitNames.Count != wantedUnits.Count)
            return Prepared.Refused(AuthorCocktailOutcome.InvalidLine);

        // JJ-041: stored in ounces whatever it was written in. The whole recipe goes in at once,
        // because a part only has a volume against the other parts.
        var measured = BarMeasure.ToStored(
            [.. lines.Select(l => new BarMeasure.Line(l.Amount, l.UnitId is { } unit ? unitNames[unit] : null))]);
        var ounce = await units.Query()
            .Where(u => u.Name == BarMeasure.Ounce)
            .Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (ounce is null && measured.Any(m => m.Unit == BarMeasure.Ounce))
            return Prepared.Refused(AuthorCocktailOutcome.InvalidLine);

        return new Prepared(null, name, [.. lines.Select((l, position) => new CocktailIngredient
        {
            TenantId = tenantId,
            IngredientId = l.IngredientId,
            Amount = measured[position].Amount,
            UnitId = measured[position].Unit == BarMeasure.Ounce ? ounce : l.UnitId,
            IsRequired = l.IsRequired,
            Role = l.Role,
            // The array's order is the recipe's order.
            DisplayOrder = position,
            Notes = Tidy(l.Notes),
        })]);
    }

    private sealed record Prepared(AuthorCocktailOutcome? Refusal, string Name, List<CocktailIngredient> Lines)
    {
        public static Prepared Refused(AuthorCocktailOutcome outcome) => new(outcome, string.Empty, []);
    }

    private static string? Tidy(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// Glass and method, when given. Both are optional by design (JJ-034) — "not stated" is a fact
    /// about a recipe, not a gap to fill with a plausible guess.
    /// </summary>
    private async Task<bool> LookupsExistAsync(
        Guid? glassId, Guid? methodId, CancellationToken cancellationToken)
    {
        if (glassId is { } glass
            && !await glasses.Query().AnyAsync(g => g.Id == glass, cancellationToken))
            return false;

        return methodId is not { } method
               || await methods.Query().AnyAsync(m => m.Id == method, cancellationToken);
    }

    /// <summary>
    /// The things wrong with a line that need no database to see. Returns null when every line is
    /// shaped like a line.
    /// </summary>
    private static AuthorCocktailOutcome? LineShape(IReadOnlyList<AuthorLineRequest> lines)
    {
        foreach (var line in lines)
        {
            // Zero of something is not a measurement, and a negative one is a typo.
            if (line.Amount is <= 0) return AuthorCocktailOutcome.InvalidLine;

            // A unit with no amount renders as nothing at all — the display formatter shows an empty
            // string when there is no amount — so it would be a line its author could never see. The
            // other way round is fine and means what it says: one egg.
            if (line.Amount is null && line.UnitId is not null) return AuthorCocktailOutcome.InvalidLine;

            if (line.Notes is { Length: > MaxNotesLength }) return AuthorCocktailOutcome.InvalidLine;
        }

        return null;
    }
}
