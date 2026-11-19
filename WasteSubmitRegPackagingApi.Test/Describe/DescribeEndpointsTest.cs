using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WasteSubmitRegPackagingApi.Describe.Endpoints;

namespace WasteSubmitRegPackagingApi.Test.Describe;

public class DescribeEndpointsTest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Returns_the_service_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/describe", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DescribeResponse>(JsonOptions, cancellationToken);
        Assert.Equal(new DescribeResponse("waste-submit-reg-packaging-api"), body);
    }
}
