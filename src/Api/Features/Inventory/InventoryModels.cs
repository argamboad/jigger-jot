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
