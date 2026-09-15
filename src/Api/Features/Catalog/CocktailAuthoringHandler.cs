using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// A household writes a cocktail from scratch (AUTHORING-1, FEATURES §14).
/// <para>
/// The flow's last line — "immediately participates in makeable / filtering like any other cocktail"
/// — costs nothing to honour, and that is the design paying out rather than luck. Both are derived
/// from the recipe lines at query time (JJ-003, JJ-014), so a drink written a second ago is exactly
/// as visible to the engine as one seeded from a 1930 book. There is no index to rebuild and no tag
/// to remember to set.
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

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            return new AuthorCocktailResult(AuthorCocktailOutcome.InvalidName);

        // A cocktail with no lines is not merely empty, it is misleading: makeability counts
        // UNSATISFIED required lines, so a drink with none is "makeable" by the letter of the rule
        // and would sit in the list of things you can pour tonight, made of nothing.
        if (request.Lines is not { Count: > 0 } lines)
            return new AuthorCocktailResult(AuthorCocktailOutcome.NoLines);

        if (!await LookupsExistAsync(request.GlassTypeId, request.MethodId, cancellationToken))
            return new AuthorCocktailResult(AuthorCocktailOutcome.UnknownLookup);

        if (LineShape(lines) is { } badShape)
            return new AuthorCocktailResult(badShape);

        // Query(), so this is the shared catalog plus this household's own ingredients and nothing
        // else (JJ-031). The same ingredient may appear on several lines (FEATURES §14), so the
        // check is on the distinct set.
        var wanted = lines.Select(l => l.IngredientId).Distinct().ToList();
        var visible = await ingredients.Query()
            .Where(i => wanted.Contains(i.Id)).CountAsync(cancellationToken);
        if (visible != wanted.Count)
            return new AuthorCocktailResult(AuthorCocktailOutcome.UnknownIngredient);

        var wantedUnits = lines.Where(l => l.UnitId is not null).Select(l => l.UnitId!.Value).Distinct().ToList();
        var unitNames = await units.Query()
            .Where(u => wantedUnits.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
        if (unitNames.Count != wantedUnits.Count)
            return new AuthorCocktailResult(AuthorCocktailOutcome.InvalidLine);

        // JJ-041: stored in ounces whatever it was written in. The whole recipe goes in at once,
        // because a part only has a volume against the other parts.
        var measured = BarMeasure.ToStored(
            [.. lines.Select(l => new BarMeasure.Line(l.Amount, l.UnitId is { } unit ? unitNames[unit] : null))]);
        var ounce = await units.Query()
            .Where(u => u.Name == BarMeasure.Ounce)
            .Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (ounce is null && measured.Any(m => m.Unit == BarMeasure.Ounce))
            return new AuthorCocktailResult(AuthorCocktailOutcome.InvalidLine);

        var cocktail = new Cocktail
        {
            // Set by hand on the cocktail AND every line: nothing stamps these tables (JJ-031), and a
            // row written without one would land in the shared catalog where every household on the
            // platform would see it.
            TenantId = tenantId,
            Name = name,
            GlassTypeId = request.GlassTypeId,
            MethodId = request.MethodId,
            ServingType = request.ServingType,
            Instructions = string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions.Trim(),

            // No SourceId and no ForkedFromCocktailId: this was written here, not transcribed from a
            // book (JJ-032) and not copied from another recipe (JJ-013).
            Lines = [.. lines.Select((l, position) => new CocktailIngredient
            {
                TenantId = tenantId,
                IngredientId = l.IngredientId,
                Amount = measured[position].Amount,
                UnitId = measured[position].Unit == BarMeasure.Ounce ? ounce : l.UnitId,
                IsRequired = l.IsRequired,
                Role = l.Role,
                // The array's order is the recipe's order.
                DisplayOrder = position,
                Notes = string.IsNullOrWhiteSpace(l.Notes) ? null : l.Notes.Trim(),
            })],
        };

        await cocktails.AddAsync(cocktail, cancellationToken);
        await cocktails.SaveChangesAsync(cancellationToken);

        return new AuthorCocktailResult(AuthorCocktailOutcome.Created, cocktail.Id);
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
