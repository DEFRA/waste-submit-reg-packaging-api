using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WasteSubmitRegPackagingApi.Test.OpenApi;

public class OpenApiTest
{
    [Fact]
    public async Task OpenApi_document_describes_both_packaging_endpoints()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var paths = json.RootElement.GetProperty("paths");

        Assert.Equal(
            "Every approved submission for a year",
            paths.GetProperty("/packaging/{year}/aggregated-submissions").GetProperty("get").GetProperty("summary").GetString());
        Assert.Equal(
            "Approved packaging data for one submitter",
            paths.GetProperty("/packaging/{year}/organisation/{organisationId}/aggregated-submission").GetProperty("get").GetProperty("summary").GetString());
    }

    [Fact]
    public async Task Swagger_ui_is_served()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Swagger_is_off_unless_enabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Swagger:Enabled", "false"));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/index.html", cancellationToken)).StatusCode);
    }
}
