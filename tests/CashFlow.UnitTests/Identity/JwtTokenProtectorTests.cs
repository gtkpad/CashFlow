using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CashFlow.Identity.API.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace CashFlow.UnitTests.Identity;

public class JwtTokenProtectorTests
{
    private const string SigningKey = "this-is-a-test-signing-key-that-is-at-least-32-bytes-long!";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    private readonly JwtTokenProtector _sut = new(SigningKey, Issuer, Audience,
        NullLogger<JwtTokenProtector>.Instance);

    [Fact]
    public void Protect_ShouldReturnValidJwtString()
    {
        var ticket = CreateTicket(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        var token = _sut.Protect(ticket);

        token.Should().StartWith("eyJ");
    }

    [Fact]
    public void Protect_ShouldIncludeClaimsInToken()
    {
        var ticket = CreateTicket(
            new Claim(ClaimTypes.NameIdentifier, "user-42"),
            new Claim(ClaimTypes.Email, "user@example.com"));

        var token = _sut.Protect(ticket);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.NameId && c.Value == "user-42");
        jwt.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Email && c.Value == "user@example.com");
    }

    [Fact]
    public void Protect_ShouldSetCorrectIssuerAndAudience()
    {
        var ticket = CreateTicket(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        var token = _sut.Protect(ticket);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Contain(Audience);
    }

    [Fact]
    public void Unprotect_WithValidToken_ShouldReturnAuthenticationTicket()
    {
        var ticket = CreateTicket(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        var token = _sut.Protect(ticket);
        var result = _sut.Unprotect(token);

        result.Should().NotBeNull();
        result!.AuthenticationScheme.Should().Be(IdentityConstants.BearerScheme);
        result.Properties.ExpiresUtc.Should().NotBeNull();
    }

    [Fact]
    public void Unprotect_WithValidToken_ShouldPreserveUserId()
    {
        var userId = "user-99";
        var ticket = CreateTicket(new Claim(ClaimTypes.NameIdentifier, userId));

        var token = _sut.Protect(ticket);
        var result = _sut.Unprotect(token);

        result.Should().NotBeNull();
        result!.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(userId);
    }

    [Fact]
    public void Unprotect_WithNullToken_ShouldReturnNull()
    {
        var result = _sut.Unprotect(null);

        result.Should().BeNull();
    }

    [Fact]
    public void Unprotect_WithInvalidToken_ShouldReturnNull()
    {
        var result = _sut.Unprotect("this-is-not-a-valid-jwt-token");

        result.Should().BeNull();
    }

    [Fact]
    public void Unprotect_WithWrongSigningKey_ShouldReturnNull()
    {
        var otherProtector = new JwtTokenProtector(
            "a-completely-different-signing-key-that-is-also-long-enough!", Issuer, Audience,
            NullLogger<JwtTokenProtector>.Instance);

        var ticket = CreateTicket(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        var token = otherProtector.Protect(ticket);

        var result = _sut.Unprotect(token);

        result.Should().BeNull();
    }

    private static AuthenticationTicket CreateTicket(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, IdentityConstants.BearerScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        return new AuthenticationTicket(principal, properties, IdentityConstants.BearerScheme);
    }
}
