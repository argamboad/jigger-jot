using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JiggerJot.Api.Authentication;
using JiggerJot.Api.Models;
using JiggerJot.Api.Services;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Controllers;

/// <summary>
/// Signed-in account surface: profile, erasure, locale/theme preferences, and OAuth login links.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AccountController(
    IUserService userService,
    ISessionService sessionService,
    IAccountErasureService accountErasure,
    IUserLoginRepository userLoginRepository,
    ILinkTokenService linkTokenService,
    ILogger<AccountController> logger) : AuthControllerBase
{
    private static readonly string[] SupportedLocales = ["en", "es", "fr", "de", "pt"];
    private static readonly string[] SupportedThemes = ["light", "dark", "system"];

    /// <summary>
    /// Returns the signed-in user's display info for the client top bar.
    /// Authenticated via the JWT Bearer access token.
    /// </summary>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized(new ErrorResponse("invalid_token", "Invalid user identity"));

        var user = await userService.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
            return Unauthorized(new ErrorResponse("user_not_found", "User not found"));

        var (_, tenantName) = await sessionService.ResolveTenantAsync(user.Id, cancellationToken);
        return Ok(new UserProfileResponse
        {
            UserName = user.DisplayName ?? user.Email,
            TenantName = tenantName ?? string.Empty,
            // Null travels as null on purpose: "never chose" is a real state that the unit switcher
            // shows as "As written", and collapsing it to a default here would erase the difference.
            PreferredUnitSystem = user.PreferredUnitSystem?.ToString(),
        });
    }

    /// <summary>
    /// Deletes the signed-in user's account and personal data (GDPR-2, ADR-011). Removes identity/PII
    /// in one audited transaction; honors the single-owner invariant — an owner with other members must
    /// transfer first, and a solo owner must confirm dissolution (which wipes the tenant's data).
    /// </summary>
    [HttpDelete("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> DeleteAccount([FromQuery(Name = "confirm_dissolve")] bool confirmDissolve, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new ErrorResponse("invalid_token", "Invalid user identity"));

        var result = await accountErasure.EraseAsync(userId, confirmDissolve, cancellationToken);
        return result switch
        {
            EraseAccountResult.Erased => NoContent(),
            EraseAccountResult.MustTransferFirst => BadRequest(new ErrorResponse(
                "must_transfer_first", "Transfer ownership before deleting your account — other members remain")),
            EraseAccountResult.DissolveConfirmationRequired => Conflict(new ErrorResponse(
                "confirmation_required",
                "Deleting your account dissolves your household and permanently deletes its data. "
                + "Re-send with confirm_dissolve=true to proceed.")),
            _ => Unauthorized(new ErrorResponse("user_not_found", "User not found")),
        };
    }

    /// <summary>
    /// Saves the signed-in user's preferred UI language so it follows them across
    /// devices. The new value lands in the JWT on the next refresh.
    /// </summary>
    [HttpPut("locale")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SetLocale([FromBody] LocaleRequest req, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (ImpersonatedPrefWrite() is { } denied) return denied;

        var locale = req.Locale?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(locale) || !SupportedLocales.Contains(locale))
            return BadRequest(new ErrorResponse("unsupported_locale", "Unsupported locale."));

        await userService.UpdateLocaleAsync(userId, locale, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Saves the signed-in user's preferred UI theme so it follows them across devices.
    /// "system" (follow the OS scheme) is a stored preference like the others — null means
    /// "never chose", which lets sign-in adopt a device-local choice (PREFS-1, ADR-022).
    /// The new value lands in the JWT on the next refresh.
    /// </summary>
    [HttpPut("theme")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SetTheme([FromBody] ThemeRequest req, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (ImpersonatedPrefWrite() is { } denied) return denied;

        var theme = req.Theme?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(theme) || !SupportedThemes.Contains(theme))
            return BadRequest(new ErrorResponse("unsupported_theme", "Unsupported theme."));

        await userService.UpdateThemeAsync(userId, theme, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Saves how the signed-in user wants recipe amounts shown, so it follows them across devices
    /// (JJ-008). An empty or absent value clears the preference back to "never chose", which shows
    /// every recipe as its book wrote it — that is a choice a reader can make, not just a default
    /// they start with.
    /// </summary>
    [HttpPut("unit-system")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SetUnitSystem(
        [FromBody] UnitSystemRequest req, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (ImpersonatedPrefWrite() is { } denied) return denied;

        UnitSystem? unitSystem = null;
        if (!string.IsNullOrWhiteSpace(req.UnitSystem))
        {
            // Neutral is a property of a UNIT, not something a reader can prefer: "show me everything
            // in dashes" is not a request anyone can act on.
            if (!Enum.TryParse<UnitSystem>(req.UnitSystem.Trim(), ignoreCase: true, out var parsed)
                || parsed == UnitSystem.Neutral)
            {
                return BadRequest(new ErrorResponse(
                    "unsupported_unit_system", "Unsupported unit system."));
            }
            unitSystem = parsed;
        }

        await userService.UpdatePreferredUnitSystemAsync(userId, unitSystem, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Server-side half of the no-pref-writes-while-impersonating guard (ADR-022 / v3 audit ADM-8): an
    /// impersonation token must not mutate the target's account preferences — previously only the Blazor
    /// client enforced this, one client bug away from failing. Returns the ready-made 403, or null to proceed.
    /// </summary>
    private IActionResult? ImpersonatedPrefWrite() =>
        User.IsImpersonation()
            ? StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse(
                "impersonation_not_allowed", "Preferences cannot be changed from an impersonation session"))
            : null;

    // ── Account linking ──────────────────────────────────────────────────────

    /// <summary>
    /// Lists the OAuth providers linked to the signed-in account (for the settings page).
    /// </summary>
    [HttpGet("logins")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logins(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var logins = await userLoginRepository.GetForUserAsync(userId, cancellationToken);
        return Ok(logins.Select(l => new { provider = l.Provider, linkedAt = l.CreatedAt }));
    }

    /// <summary>
    /// Issues a single-use link token for the current user and returns the provider
    /// sign-in URL carrying it. The client full-page-navigates there to link the
    /// provider to this account (no new user is created).
    /// </summary>
    [HttpPost("link/{provider}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult StartLink(string provider)
    {
        provider = provider.ToLowerInvariant();
        if (!AuthProviders.IsSupported(provider))
            return BadRequest(new ErrorResponse("unsupported_provider", "Unknown provider."));
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var token = linkTokenService.Issue(userId);
        var url = $"{Request.Scheme}://{Request.Host}/api/auth/login/{provider}" +
                  $"?link_token={Uri.EscapeDataString(token)}";
        // url: web full-page navigates to it. token: native carries it through the
        // loopback OAuth flow (native/login?...&link_token=) instead.
        return Ok(new { url, token });
    }

    /// <summary>
    /// Unlinks an OAuth provider from the account. Sign-in by email (magic link / OTP)
    /// always remains available, so removing a provider can't lock the user out.
    /// </summary>
    [HttpDelete("logins/{provider}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Unlink(string provider, CancellationToken cancellationToken)
    {
        provider = provider.ToLowerInvariant();
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var login = await userLoginRepository.GetByProviderForUserAsync(userId, provider, cancellationToken);
        if (login is null) return NotFound();

        await userLoginRepository.DeleteAsync(login, cancellationToken);
        logger.LogInformation("Unlinked {Provider} from user {UserId}", provider, userId);
        return Ok();
    }
}
