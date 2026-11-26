using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WasteSubmitRegPackagingApi.Describe.Endpoints;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.Platform;

/// <summary>
/// The acceptance criteria for service-to-service authentication, over HTTP: unauthenticated callers are
/// refused, authenticated callers are accepted. Every way a header can be wrong is in BasicAuthenticationHandlerTests,
/// and EndpointConventionsTest checks every endpoint asks for credentials.
/// </summary>
public class DescribeEndpointsTest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Unauthenticated_request_is_refused_with_a_basic_challenge()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Basic", challenge.Scheme);
    }

    [Fact]
    public async Task Request_with_the_wrong_secret_is_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, "wrong-secret");

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_request_returns_the_service_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DescribeResponse>(JsonOptions, cancellationToken);
        Assert.Equal(new DescribeResponse("waste-submit-reg-packaging-api"), body);
    }
}
