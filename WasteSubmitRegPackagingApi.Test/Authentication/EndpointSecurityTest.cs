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
}
