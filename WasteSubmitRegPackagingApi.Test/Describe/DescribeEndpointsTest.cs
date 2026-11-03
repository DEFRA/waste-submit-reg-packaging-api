using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WasteSubmitRegPackagingApi.Describe.Endpoints;

namespace WasteSubmitRegPackagingApi.Test.Describe;

/// <summary>
/// The acceptance criteria for service-to-service authentication: unauthenticated callers are
/// refused, authenticated callers are accepted.
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
    public async Task Scheme_name_is_case_insensitive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();
        var credentials = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret).Parameter;
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"basic {credentials}");

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(AclTestFactory.ClientId, "wrong-secret")]
    [InlineData("UnknownClient", AclTestFactory.Secret)]
    public async Task Request_with_invalid_credentials_is_refused(string clientId, string secret)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(clientId, secret);

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Basic !!!notbase64")]
    [InlineData("Bearer some-token")]
    public async Task Request_with_malformed_or_unsupported_authorization_is_refused(string authorization)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);

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

    [Fact]
    public async Task Client_id_from_config_is_matched_case_insensitively()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            AclTestFactory.Basic(AclTestFactory.ClientId.ToLowerInvariant(), AclTestFactory.Secret);

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_client_not_allowed_the_endpoint_gets_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            AclTestFactory.Basic(AclTestFactory.NoEndpointsClientId, AclTestFactory.Secret);

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_client_without_the_read_scope_is_forbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            AclTestFactory.Basic(AclTestFactory.NoReadScopeClientId, AclTestFactory.Secret);

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
