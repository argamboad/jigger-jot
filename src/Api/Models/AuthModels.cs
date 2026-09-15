using System.Text.Json.Serialization;

namespace JiggerJot.Api.Models;

public record EmailRequest(string Email, string? Culture = null);

public record OtpVerifyRequest(string Email, string Code);

public record RefreshRequest(
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);

public record NativeExchangeRequest(
    [property: JsonPropertyName("code")] string Code);

public record LocaleRequest(
    [property: JsonPropertyName("locale")] string? Locale);

public record ThemeRequest(
    [property: JsonPropertyName("theme")] string? Theme);

/// <summary>How a reader wants recipe amounts shown: "Metric" or "Imperial". Null or empty is refused —
/// every volume is stored in ounces, so there is no "as written" to clear back to (JJ-041).</summary>
public record UnitSystemRequest(
    [property: JsonPropertyName("unitSystem")] string? UnitSystem);
