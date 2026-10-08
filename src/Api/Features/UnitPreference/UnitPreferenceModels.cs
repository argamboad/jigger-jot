using System.Text.Json.Serialization;

namespace JiggerJot.Api.Features.UnitPreference;

/// <summary>The caller's measuring system. <c>isDefault</c> = never chose (the server then shows Imperial).</summary>
public record UnitPreferenceResponse(
    [property: JsonPropertyName("unitSystem")] string UnitSystem,
    [property: JsonPropertyName("isDefault")] bool IsDefault);

/// <summary>"Metric" or "Imperial", case-insensitive. Null or empty is refused — every volume is stored in ounces, so
/// there is no "as written" to clear back to (JJ-041).</summary>
public record UpdateUnitPreferenceRequest(
    [property: JsonPropertyName("unitSystem")] string? UnitSystem);
