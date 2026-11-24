using WasteSubmitRegPackagingApi.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WasteSubmitRegPackagingApi.Test.Authentication;

public class EndpointSecurityTest
{
    [Fact]
    public async Task The_describe_endpoint_requires_the_read_policy()
    {
        // Access is decided by the client's scopes in the ACL, so /describe must keep its policy
        // and never become anonymous.
        await using var factory = new AclTestFactory();
        var describe = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Single(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "Describe");

        Assert.Null(describe.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Contains(
            describe.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorize => authorize.Policy == PolicyNames.Read);
    }

    [Theory]
    [InlineData("GetProducerMaterialTotals")]
    [InlineData("GetProducerMaterialTotalsBySubmitter")]
    public async Task Packaging_endpoints_require_the_read_policy(string endpointName)
    {
        await using var factory = new AclTestFactory();
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Single(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == endpointName);

        Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorize => authorize.Policy == PolicyNames.Read);
    }
}
