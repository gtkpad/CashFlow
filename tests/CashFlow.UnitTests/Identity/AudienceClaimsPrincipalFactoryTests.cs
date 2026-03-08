using System.Security.Claims;
using CashFlow.Identity.API;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CashFlow.UnitTests.Identity;

public class AudienceClaimsPrincipalFactoryTests
{
    private static UserManager<IdentityUser> CreateMockUserManager()
    {
        var store = Substitute.For<IUserStore<IdentityUser>>();
        return Substitute.For<UserManager<IdentityUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static AudienceClaimsPrincipalFactory CreateFactory(
        string? audience, UserManager<IdentityUser>? userManager = null)
    {
        userManager ??= CreateMockUserManager();

        var configData = new Dictionary<string, string?>();
        if (audience is not null)
            configData["Identity:Audience"] = audience;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var identityOptions = Options.Create(new IdentityOptions());

        return new AudienceClaimsPrincipalFactory(userManager, identityOptions, configuration);
    }

    [Fact]
    public async Task GenerateClaimsAsync_WithAudienceConfigured_ShouldAddAudClaim()
    {
        var factory = CreateFactory("cashflow-api");
        var user = new IdentityUser { UserName = "test@test.com", Id = Guid.NewGuid().ToString() };

        var principal = await factory.CreateAsync(user);

        principal.Claims.Should().Contain(c => c.Type == "aud" && c.Value == "cashflow-api");
    }

    [Fact]
    public async Task GenerateClaimsAsync_WithoutAudienceConfigured_ShouldNotAddAudClaim()
    {
        var factory = CreateFactory(audience: null);
        var user = new IdentityUser { UserName = "test@test.com", Id = Guid.NewGuid().ToString() };

        var principal = await factory.CreateAsync(user);

        principal.Claims.Should().NotContain(c => c.Type == "aud");
    }

    [Fact]
    public async Task GenerateClaimsAsync_ShouldPreserveExistingClaims()
    {
        var factory = CreateFactory("cashflow-api");
        var user = new IdentityUser { UserName = "test@test.com", Id = Guid.NewGuid().ToString() };

        var principal = await factory.CreateAsync(user);

        principal.Identity.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
    }
}
