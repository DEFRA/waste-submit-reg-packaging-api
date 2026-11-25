using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WasteSubmitRegPackagingApi.Authentication;

namespace WasteSubmitRegPackagingApi.Test.ArchitectureTests;

/// <summary>
/// Rules every endpoint must meet, read from the endpoints the host maps. A new endpoint is covered without being
/// added here, so no endpoint needs its own "without credentials it is a 401" test.
/// </summary>
public class EndpointConventionsTest
{
    // CDP polls the health checks, and an authentication failure would mark the service unhealthy. The OpenAPI
    // documents are only mapped where Swagger:Enabled is set. Nothing else may be anonymous.
    private static readonly string[] s_anonymousRoutes = ["/health", "/health/db", "/openapi/{documentName}.json"];

    [Fact]
    public async Task Every_endpoint_requires_the_read_or_write_policy_except_health_and_the_openapi_documents()
    {
        await using var factory = new AclTestFactory();
        var endpoints = Endpoints(factory);

        var unsecured = endpoints.Where(endpoint => !RequiresReadOrWrite(endpoint)).Select(Route).Order();

        Assert.Equal(s_anonymousRoutes.Order(), unsecured);
    }

    [Fact]
    public async Task The_health_endpoints_are_explicitly_anonymous_and_have_no_authorization_requirement()
    {
        await using var factory = new AclTestFactory();
        var health = Endpoints(factory).Where(endpoint => Route(endpoint).StartsWith("/health", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, health.Count);
        Assert.All(health, endpoint =>
        {
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            Assert.Empty(endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());
        });
    }

    [Fact]
    public async Task There_is_no_fallback_authorization_policy()
    {
        // A fallback policy would apply to every endpoint without its own, including any health
        // check someone forgets to mark anonymous. Endpoints opt in to authentication instead.
        await using var factory = new AclTestFactory();
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.Null(await policyProvider.GetFallbackPolicyAsync());
    }

    private static bool RequiresReadOrWrite(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
        && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(authorize => authorize.Policy is PolicyNames.Read or PolicyNames.Write);

    private static List<RouteEndpoint> Endpoints(AclTestFactory factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

    private static string Route(RouteEndpoint endpoint) => "/" + endpoint.RoutePattern.RawText!.TrimStart('/');
}
