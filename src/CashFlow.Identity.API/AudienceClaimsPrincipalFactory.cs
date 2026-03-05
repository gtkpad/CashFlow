using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CashFlow.Identity.API;

internal sealed class AudienceClaimsPrincipalFactory(
    UserManager<IdentityUser> userManager,
    IOptions<IdentityOptions> optionsAccessor,
    IConfiguration configuration)
    : UserClaimsPrincipalFactory<IdentityUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(IdentityUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var audience = configuration["Identity:Audience"];
        if (!string.IsNullOrEmpty(audience))
            identity.AddClaim(new Claim("aud", audience));

        return identity;
    }
}
