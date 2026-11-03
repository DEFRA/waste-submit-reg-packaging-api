using WasteSubmitRegPackagingApi.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WasteSubmitRegPackagingApi.Test.Authentication;

public class EndpointSecurityTest
{
    [Fact]
    public async Task Every_endpoint_that_requires_authorization_also_checks_allowed_endpoints()
    {
        // RequireAuthorization on its own would let any client with the scope call the endpoint,
        // whatever its AllowedEndpoints says. RequireServiceAuthorization applies both.
        await using var factory = new AclTestFactory();
        var secured = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0)
            .ToList();

        Assert.NotEmpty(secured);
        Assert.All(secured, endpoint =>
            Assert.True(
                endpoint.Metadata.GetMetadata<ServiceAuthorizationMetadata>() is not null,
                $"{endpoint.DisplayName} requires authorization without RequireServiceAuthorization."));
    }

    [Fact]
    public async Task The_describe_endpoint_is_secured()
    {
        await using var factory = new AclTestFactory();
        var describe = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Single(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "Describe");

        Assert.NotNull(describe.Metadata.GetMetadata<ServiceAuthorizationMetadata>());
    }
}
