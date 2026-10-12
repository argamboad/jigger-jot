using System.Security.Claims;
using JiggerJot.Api.Authentication;
using JiggerJot.Api.Endpoints;
using JiggerJot.Api.Services;

namespace JiggerJot.Api.Features.UnitPreference;

/// <summary>
/// <c>GET</c> / <c>PUT /api/unit-preference</c> — the caller's own measuring system (JJ-008; a slice since Arch A3,
/// jigger-jot#164, where it was <c>PUT /api/auth/unit-system</c> on the platform's account controller). Any signed-in
/// member may read and save. Like the platform's theme and locale, a write from an impersonation session is refused
/// (403 <c>impersonation_not_allowed</c>): staff looking through a member's eyes must not change what that member sees.
/// </summary>
public static class UnitPreferenceEndpoints
{
    public static IEndpointRouteBuilder MapUnitPreference(this IEndpointRouteBuilder app)
    {
        var group = app.MapTenantFeatureGroup("/api/unit-preference");

        group.MapGet("/", async (ClaimsPrincipal user, UnitPreferenceHandler handler, CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } uid) return Results.Unauthorized();
            return Results.Ok(await handler.GetAsync(uid, ct));
        });

        group.MapPut("/", async (ClaimsPrincipal user, UpdateUnitPreferenceRequest request, UnitPreferenceHandler handler, CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } uid) return Results.Unauthorized();
            if (user.IsImpersonation())
                return Results.Json(new ErrorResponse("impersonation_not_allowed", "Preferences cannot be changed from an impersonation session"), statusCode: StatusCodes.Status403Forbidden);
            var (saved, error) = await handler.UpdateAsync(uid, request, ct);
            return error is not null ? Results.BadRequest(error) : Results.Ok(saved);
        });

        return app;
    }
}
