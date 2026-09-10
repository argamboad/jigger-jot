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

/// <summary>How a reader wants recipe amounts shown. Null or empty clears the preference back to
/// "never chose", which shows every recipe as its book wrote it (JJ-007).</summary>
public record UnitSystemRequest(
    [property: JsonPropertyName("unitSystem")] string? UnitSystem);
