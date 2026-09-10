using System.Text.Json.Serialization;

namespace JiggerJot.Api.Models;

/// <summary>
/// Current user profile (GET /api/auth/me) — surfaced to the client top bar.
/// </summary>
public record UserProfileResponse
{
    [JsonPropertyName("userName")]
    public required string UserName { get; init; }

    [JsonPropertyName("tenantName")]
    public required string TenantName { get; init; }

    /// <summary>
    /// How this reader wants recipe amounts shown: "Metric", "Imperial", or null for "never chose",
    /// which shows every recipe as its book wrote it (JJ-007, JJ-008). Null is a real value here and
    /// is not collapsed to a default — the unit switcher offers it as a choice.
    /// </summary>
    [JsonPropertyName("preferredUnitSystem")]
    public string? PreferredUnitSystem { get; init; }
}
