namespace JiggerJot.Core.Catalog;

/// <summary>How a household would satisfy one recipe line, right now.</summary>
public enum LineAvailability
{
    /// <summary>The exact ingredient is on the shelf.</summary>
    Have,

    /// <summary>Not the exact ingredient, but something the graph allows in its place (JJ-004, JJ-006).</summary>
    Substitute,

    /// <summary>Nothing on the shelf covers this line.</summary>
    Missing,
}

/// <summary>Where a whole cocktail stands against a shelf (JJ-003, JJ-019).</summary>
public enum MakeabilityStatus
{
    /// <summary>Every required line is satisfied. Pour it tonight.</summary>
    Makeable,

    /// <summary>Exactly one required line is unsatisfied — the shopping driver (FEATURES §10).</summary>
    AlmostMakeable,

    /// <summary>More than one required line is unsatisfied.</summary>
    NotMakeable,
}

/// <summary>How one line stands, and what would be poured if it stands on a substitute.</summary>
/// <param name="SubstituteIngredientId">Set only when <paramref name="Availability"/> is
/// <see cref="LineAvailability.Substitute"/> — the bottle actually reached for.</param>
public record LineMakeability(LineAvailability Availability, Guid? SubstituteIngredientId = null);

/// <summary>
/// The derived rules behind "what can I make" and "what am I one ingredient away from" (JJ-003,
/// JJ-019), stated once, for one cocktail at a time.
/// <para>
/// This lives in Core with no database near it for the same reason <see cref="AmountDisplay"/> does:
/// the rules are small, each one is a judgement, and every front end and every slice should inherit
/// them rather than re-derive them. The browse handler still expresses the same rules as a set-based
/// SQL predicate, because a filter over a paged catalog cannot be a loop — so a test in
/// <c>Api.Tests</c> walks the whole catalog and asserts the two agree, cocktail for cocktail. That
/// test exists because two spellings of one rule is exactly how a rule drifts.
/// </para>
/// </summary>
public static class Makeability
{
    /// <summary>
    /// How this line stands against the shelf.
    /// </summary>
    /// <param name="available">Ingredient ids the household has ticked.</param>
    /// <param name="substitutes">What may be poured when a recipe asks for a given ingredient, keyed
    /// by the ingredient <b>asked for</b>. Directed on purpose (JJ-006): cognac stands in for brandy
    /// and brandy does not stand in for cognac, so this map is never read backwards. Order the values
    /// before calling if the choice among several needs to be stable — the first available one wins.</param>
    public static LineMakeability ForLine(
        Guid ingredientId,
        IReadOnlySet<Guid> available,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> substitutes)
    {
        // The exact bottle always beats a substitute, even when both are on the shelf. Being told to
        // pour Curaçao while the Cointreau sits there would be absurd.
        if (available.Contains(ingredientId)) return new LineMakeability(LineAvailability.Have);

        if (substitutes.TryGetValue(ingredientId, out var allowed))
            foreach (var candidate in allowed)
                if (available.Contains(candidate))
                    return new LineMakeability(LineAvailability.Substitute, candidate);

        return new LineMakeability(LineAvailability.Missing);
    }

    /// <summary>
    /// Where the whole drink stands, counting <b>only required lines</b> — an unstocked garnish never
    /// blocks anything and is never what you are one ingredient short of (JJ-009).
    /// </summary>
    public static MakeabilityStatus Overall(IEnumerable<(bool IsRequired, LineAvailability Availability)> lines)
    {
        var short_ = lines.Count(l => l.IsRequired && l.Availability == LineAvailability.Missing);

        return short_ switch
        {
            0 => MakeabilityStatus.Makeable,
            // MVP fixes N = 1 (DATA_MODEL, JJ-019). Two away is a wish list, not a shopping list.
            1 => MakeabilityStatus.AlmostMakeable,
            _ => MakeabilityStatus.NotMakeable,
        };
    }
}
