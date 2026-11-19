using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WasteSubmitRegPackagingApi.Test.OpenApi;

/// <summary>
/// Deployed, UnpublishedEndpoints:Enabled is false: unpublished (sample) endpoints are a 404, published ones still work.
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
    public async Task A_published_endpoint_still_works_when_unpublished_endpoints_are_off()
    {
        await using var factory = AppWithUnpublishedEndpointsOff();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/describe", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplicationFactory<Program> AppWithUnpublishedEndpointsOff() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("UnpublishedEndpoints:Enabled", "false"));
}
