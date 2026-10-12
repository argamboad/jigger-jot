using JiggerJot.Api.Services;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiggerJot.Api.Features.UnitPreference;

/// <summary>
/// Reads and upserts the caller's single <see cref="UserUnitPreference"/> row (Arch A3, jigger-jot#164). A read never
/// writes — a reader who never chose gets Imperial flagged <c>isDefault</c>. Rows are keyed by user, so every path is
/// constrained by the caller's id, never by the household.
/// </summary>
public sealed class UnitPreferenceHandler(IRepository<UserUnitPreference> preferences, TimeProvider clock)
{
    public async Task<UnitPreferenceResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await preferences.Query().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return row is null
            ? new UnitPreferenceResponse(nameof(UnitSystem.Imperial), IsDefault: true)
            : new UnitPreferenceResponse(row.UnitSystem.ToString(), IsDefault: false);
    }

    public async Task<(UnitPreferenceResponse? Preference, ErrorResponse? Error)> UpdateAsync(Guid userId, UpdateUnitPreferenceRequest request, CancellationToken cancellationToken)
    {
        // Neutral is a property of a UNIT, not something a reader can prefer: "show me everything in dashes" is not a
        // request anyone can act on. A bare number parses as an enum value, so it is held to the defined ones too.
        if (string.IsNullOrWhiteSpace(request.UnitSystem)
            || !Enum.TryParse<UnitSystem>(request.UnitSystem.Trim(), ignoreCase: true, out var value)
            || !Enum.IsDefined(value)
            || value == UnitSystem.Neutral)
        {
            return (null, new ErrorResponse("unsupported_unit_system", "Unsupported unit system."));
        }

        var now = clock.GetUtcNow();
        var row = await preferences.Query().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var created = row is null;
        row ??= new UserUnitPreference { UserId = userId, CreatedAt = now };
        row.UnitSystem = value;
        row.UpdatedAt = now;
        if (created) await preferences.AddAsync(row, cancellationToken);
        else preferences.Update(row);
        try
        {
            await preferences.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (created)
        {
            // Two first saves raced; the unique (UserId) index let exactly one insert win — apply on top of it.
            preferences.Remove(row);
            var winner = await preferences.Query().FirstAsync(p => p.UserId == userId, cancellationToken);
            winner.UnitSystem = value;
            winner.UpdatedAt = now;
            preferences.Update(winner);
            await preferences.SaveChangesAsync(cancellationToken);
        }
        return (new UnitPreferenceResponse(value.ToString(), IsDefault: false), null);
    }
}
