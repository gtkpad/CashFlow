using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Identity.API.Auth;

internal sealed class JwtTokenProtector(string signingKey, string issuer, string audience)
    : ISecureDataFormat<AuthenticationTicket>
{
    private readonly SymmetricSecurityKey _key = new(Encoding.UTF8.GetBytes(signingKey));

    public string Protect(AuthenticationTicket data) => Protect(data, null);

    public string Protect(AuthenticationTicket data, string? purpose)
    {
        var claims = data.Principal.Claims.ToList();
        var expires = data.Properties.ExpiresUtc?.UtcDateTime ?? DateTime.UtcNow.AddHours(1);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    public AuthenticationTicket? Unprotect(string? protectedText) => Unprotect(protectedText, null);

    public AuthenticationTicket? Unprotect(string? protectedText, string? purpose)
    {
        if (string.IsNullOrEmpty(protectedText))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key
            };

            var principal = handler.ValidateToken(protectedText, validationParameters, out var validatedToken);

            var properties = new AuthenticationProperties
            {
                ExpiresUtc = validatedToken.ValidTo
            };

            return new AuthenticationTicket(principal, properties, IdentityConstants.BearerScheme);
        }
        catch
        {
            return null;
        }
    }
}
