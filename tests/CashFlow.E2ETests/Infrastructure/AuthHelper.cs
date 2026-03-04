using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CashFlow.E2ETests.Infrastructure;

public static class AuthHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<AuthResult> RegisterAndLoginAsync(HttpClient client)
    {
        var email = $"test-{Guid.NewGuid():N}@example.com";
        var password = "TestPass123!";

        // Register
        var registerResponse = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email,
            password
        });
        registerResponse.EnsureSuccessStatusCode();

        // Login
        var loginResponse = await client.PostAsJsonAsync("/api/identity/login", new
        {
            email,
            password
        });
        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content
            .ReadFromJsonAsync<LoginResponse>(JsonOptions);
        var accessToken = loginResult?.AccessToken
            ?? throw new InvalidOperationException(
                "Login response did not contain an access token");

        // Decode JWT payload to extract the user ID (sub claim) without
        // adding a dependency on System.IdentityModel.Tokens.Jwt
        var userId = ExtractSubClaim(accessToken);

        return new AuthResult(accessToken, userId);
    }

    private static string ExtractSubClaim(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            throw new InvalidOperationException(
                $"Invalid JWT format (expected 3 parts, got {parts.Length})");

        // JWT payload is base64url-encoded
        var payload = parts[1];
        // Pad to multiple of 4 for standard base64
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("sub", out var sub))
            return sub.GetString()
                   ?? throw new InvalidOperationException("sub claim is null");

        // ASP.NET Identity may use the full claim URI
        if (doc.RootElement.TryGetProperty(
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                out var nameId))
            return nameId.GetString()
                   ?? throw new InvalidOperationException("nameidentifier claim is null");

        throw new InvalidOperationException(
            $"Could not extract user ID from JWT. Payload: {json}");
    }

    public record AuthResult(string AccessToken, string UserId);

    private record LoginResponse(
        [property: JsonPropertyName("tokenType")] string TokenType,
        [property: JsonPropertyName("accessToken")] string AccessToken,
        [property: JsonPropertyName("expiresIn")] int ExpiresIn,
        [property: JsonPropertyName("refreshToken")] string RefreshToken);
}
