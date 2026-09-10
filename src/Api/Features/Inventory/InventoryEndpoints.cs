using JiggerJot.Api.Endpoints;
using JiggerJot.Api.Services;

namespace JiggerJot.Api.Features.Inventory;

/// <summary>
/// The shelf's read and write surface (INV). Registered through <c>MapTenantFeatureGroup</c>, which
/// applies the shared tenant-API auth policy so the slice never re-spells authorization.
/// </summary>
public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventory(this IEndpointRouteBuilder app)
    {
        var group = app.MapTenantFeatureGroup("/api/inventory");

        group.MapGet("/", async (InventoryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.ListAsync(ct)));

        group.MapGet("/categories", async (InventoryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.CategoriesAsync(ct)));

        // INV-2, FEATURES §8: add a custom ingredient inline. Under /api/inventory rather than a
        // catalog route because this is the shelf's own affordance — the person is standing at their
        // checklist holding a bottle the catalog does not know about.
        group.MapPost("/ingredients", async (
            AddIngredientRequest request,
            InventoryHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.AddIngredientAsync(request, ct);

            return result.Outcome switch
            {
                AddIngredientOutcome.Created =>
                    Results.Created($"/api/inventory/ingredients/{result.Item!.Id}", result.Item),

                // 409 with the id of what is already there, so the client can offer to tick that
                // instead of leaving someone to hunt for a name they just typed.
                AddIngredientOutcome.AlreadyExists => Results.Conflict(new IngredientExistsResponse(
                    "ingredient_exists",
                    "That ingredient is already on your shelf",
                    result.ExistingIngredientId!.Value)),

                AddIngredientOutcome.InvalidName => Results.BadRequest(new ErrorResponse(
                    "invalid_name", "An ingredient name is required")),

                _ => Results.BadRequest(new ErrorResponse(
                    "invalid_category", "Choose a category, and a subcategory that belongs to it")),
            };
        });

        group.MapPut("/{ingredientId:guid}", async (
            Guid ingredientId,
            SetAvailabilityRequest request,
            InventoryHandler handler,
            CancellationToken ct) =>
        {
            // 404 rather than 403 for an ingredient this household cannot see: the filter simply does
            // not return it, and a different answer would let someone probe ids to discover another
            // household's custom ingredients.
            return await handler.SetAsync(ingredientId, request.IsAvailable, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });

        return app;
    }
}
