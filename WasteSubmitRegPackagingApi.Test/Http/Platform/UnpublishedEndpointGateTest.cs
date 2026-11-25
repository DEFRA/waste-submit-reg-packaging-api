using System.Net;

namespace WasteSubmitRegPackagingApi.Test.Http.Platform;

/// <summary>
/// Deployed, UnpublishedEndpoints:Enabled is false: unpublished (sample) endpoints are a 404, even without credentials,
/// and published ones still work.
/// </summary>
public class UnpublishedEndpointGateTest
{
    [Fact]
    public async Task An_unpublished_endpoint_is_not_found_when_unpublished_endpoints_are_off()
    {
        await using var factory = AppWithUnpublishedEndpointsOff();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/registrations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_unpublished_document_is_not_found_when_unpublished_endpoints_are_off()
    {
        await using var factory = AppWithUnpublishedEndpointsOff();
        using var client = factory.CreateClient();

        var unpublished = await client.GetAsync("/openapi/unpublished.json", TestContext.Current.CancellationToken);
        var v1 = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, unpublished.StatusCode);
        Assert.Equal(HttpStatusCode.OK, v1.StatusCode);
    }

    [Fact]
    public async Task A_published_endpoint_still_works_when_unpublished_endpoints_are_off()
    {
        await using var factory = AppWithUnpublishedEndpointsOff();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);

        var response = await client.GetAsync("/describe", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static AclTestFactory AppWithUnpublishedEndpointsOff() =>
        new(new Dictionary<string, string?> { ["UnpublishedEndpoints:Enabled"] = "false" });
}
