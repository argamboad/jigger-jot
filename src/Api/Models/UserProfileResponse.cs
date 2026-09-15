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
    /// How this reader reads recipe amounts: "Metric" or "Imperial". A reader who never chose is sent
    /// "Imperial", because ounces are what is stored (JJ-041, JJ-008) — said here once rather than
    /// known by every client.
    /// </summary>
    [JsonPropertyName("preferredUnitSystem")]
    public string? PreferredUnitSystem { get; init; }
}
