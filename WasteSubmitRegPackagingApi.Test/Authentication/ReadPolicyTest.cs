using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackagingApi.Authentication;

namespace WasteSubmitRegPackagingApi.Test.Authentication;

/// <summary>
/// Every valid ACL client has the read scope (it's the only scope), so no configured client can
/// reach the "authenticated but forbidden" path over HTTP. This checks the policy itself still
/// refuses a client without the scope; EndpointSecurityTest checks every endpoint uses it.
/// </summary>
public class ReadPolicyTest
{
    private static async Task<bool> IsAuthorised(ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthenticationAuthorization();
        await using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(user, null, PolicyNames.Read);

        return result.Succeeded;
    }

    private static ClaimsPrincipal AuthenticatedClient(params string[] scopes) =>
        new(new ClaimsIdentity(
            [new Claim(Claims.ClientId, "client"), .. scopes.Select(scope => new Claim(Claims.Scope, scope))],
            BasicAuthenticationHandler.SchemeName));

    [Fact]
    public async Task An_authenticated_client_with_the_read_scope_is_allowed()
    {
        Assert.True(await IsAuthorised(AuthenticatedClient(Scopes.Read)));
    }

    [Fact]
    public async Task An_authenticated_client_without_the_read_scope_is_refused()
    {
        Assert.False(await IsAuthorised(AuthenticatedClient()));
        Assert.False(await IsAuthorised(AuthenticatedClient("write")));
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        Assert.False(await IsAuthorised(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
