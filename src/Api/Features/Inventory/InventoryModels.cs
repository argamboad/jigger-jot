namespace JiggerJot.Api.Features.Inventory;

/// <summary>
/// One ingredient as it appears on the shelf screen: what it is, where it files, and whether this
/// household has it.
/// </summary>
/// <param name="Category">The top-level category. The response carries it so every client groups the
/// same way — 191 ingredients is far too many to tick down a flat list, and grouping in the client
/// from an unordered list would put the same work in every future front end.</param>
/// <param name="Subcategory">Null for an ingredient that sits at the top level.</param>
/// <param name="IsOwn">True for an ingredient this household added rather than one from the shared
/// catalog.</param>
public record ShelfItem(
    Guid Id,
    string Name,
    string Category,
    string? Subcategory,
    bool IsAvailable,
    bool IsOwn);

/// <summary>Ticking or unticking one ingredient.</summary>
public record SetAvailabilityRequest(bool IsAvailable);

/// <summary>
/// Adding a household's own ingredient inline from the shelf (INV-2, FEATURES §8).
/// </summary>
/// <param name="CategoryId">A <b>top-level</b> category from <c>GET /api/inventory/categories</c>.
/// The lookups are curated and global with no household additions (JJ-022), so this references one
/// rather than naming a new one.</param>
/// <param name="SubcategoryId">Optional, and must be a child of <paramref name="CategoryId"/> —
/// filtering a parent matches all its children (JJ-016), so a mismatched pair would file the
/// ingredient where nobody looks for it.</param>
public record AddIngredientRequest(string Name, Guid CategoryId, Guid? SubcategoryId);

/// <summary>Why an add did or did not happen. Named outcomes rather than exceptions, because every
/// one of these is an ordinary thing for someone to do rather than a fault.</summary>
public enum AddIngredientOutcome
{
    Created,

    /// <summary>Something with this name is already on the shelf — the household's own, or the shared
    /// catalog's. The response carries its id so the caller can offer to tick that instead.</summary>
    AlreadyExists,

    /// <summary>Blank, or nothing but whitespace.</summary>
    InvalidName,

    /// <summary>No such category, a subcategory passed where a category belongs, or a subcategory
    /// belonging to some other category.</summary>
    InvalidCategory,
}

/// <param name="Item">The new shelf row, on <see cref="AddIngredientOutcome.Created"/> only.</param>
/// <param name="ExistingIngredientId">What was already there, on
/// <see cref="AddIngredientOutcome.AlreadyExists"/> only — a dead end otherwise, and the difference
/// between "no" and "no, tick this one".</param>
public record AddIngredientResult(
    AddIngredientOutcome Outcome,
    ShelfItem? Item = null,
    Guid? ExistingIngredientId = null);

/// <summary>One top-level category and its children, for the picker (JJ-015: two levels, always).</summary>
public record CategoryOption(Guid Id, string Name, IReadOnlyList<CategoryOption> Subcategories);

/// <summary>
/// The 409 body when the name is already on this household's shelf. The shared
/// <c>ErrorResponse</c> carries a code and a message and nothing else, and this response needs a
/// third thing: <b>which</b> row is already there, so a client can offer to tick it rather than
/// leaving someone to hunt for a name they just typed. Hence a named record rather than an anonymous
/// shape, which the slice-surface gate bans for exactly this reason.
/// </summary>
public record IngredientExistsResponse(string Error, string Message, Guid ExistingIngredientId);
