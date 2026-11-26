using System.Net;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.Platform;

/// <summary>
/// CDP polls the health checks to decide whether the container is alive. They must never require
/// authentication, whatever the caller sends. If any of these fail, CDP will treat the service as
/// unhealthy and stop routing to it. EndpointConventionsTest checks they are mapped as anonymous.
/// </summary>
public class HealthEndpointsAnonymousTest
{
    private static readonly string?[] AuthorizationHeaders =
    [
        null,
        "Basic !!!notbase64",
        "Basic",
        "not a valid header",
        AclTestFactory.Basic(AclTestFactory.ClientId, "wrong-secret").ToString(),
        AclTestFactory.Basic("UnknownClient", AclTestFactory.Secret).ToString(),
        "Bearer junk",
        AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret).ToString(),
    ];

    public static TheoryData<string?> Headers() => new(AuthorizationHeaders);

    [Theory]
    [MemberData(nameof(Headers))]
    public async Task Container_health_check_is_ok_whatever_authorization_is_sent_and_ignores_the_database(string? authorization)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory(PostgresSettings.Unreachable);
        using var client = ClientSending(factory, authorization);

        var response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Theory]
    [MemberData(nameof(Headers))]
    public async Task Database_health_check_runs_whatever_authorization_is_sent(string? authorization)
    {
        // With no database the check still has to run and report Unhealthy (503), rather than being
        // blocked by authentication (401/403) or hidden (404).
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new AclTestFactory(PostgresSettings.Unreachable);
        using var client = ClientSending(factory, authorization);

        var response = await client.GetAsync("/health/db", cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static HttpClient ClientSending(AclTestFactory factory, string? authorization)
    {
        var client = factory.CreateClient();
        if (authorization is not null)
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        return client;
    }
}
