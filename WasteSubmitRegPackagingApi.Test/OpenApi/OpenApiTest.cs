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
            "Material totals for each producer, grouped by submitter",
            paths.GetProperty("/packaging/{packagingYear}/producer-material-totals-by-submitter").GetProperty("get").GetProperty("summary").GetString());
        Assert.Equal(
            "Material totals for each producer one submitter reported for",
            paths.GetProperty("/packaging/{packagingYear}/submitter-organisation/{submitterOrganisationId}/producer-material-totals").GetProperty("get").GetProperty("summary").GetString());
    }

    [Fact]
    public async Task OpenApi_document_describes_the_api_key_and_the_actor_headers_so_swagger_can_call_the_api()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", cancellationToken));
        var root = json.RootElement;

        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("basic");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("basic", scheme.GetProperty("scheme").GetString());

        var decision = root.GetProperty("paths").GetProperty("/regulator/registrations/{registrationSubmissionId}/decisions").GetProperty("post");
        Assert.True(decision.GetProperty("security")[0].TryGetProperty("basic", out _));

        var headers = decision.GetProperty("parameters").EnumerateArray()
            .Where(p => p.GetProperty("in").GetString() == "header")
            .Select(p => p.GetProperty("name").GetString())
            .ToList();
        Assert.Equal(["X-Actor-User-Id", "X-Actor-Type", "X-Actor-Organisation-Id"], headers);

        // Queries need the API key but no actor.
        var query = root.GetProperty("paths").GetProperty("/regulator/registrations/{registrationSubmissionId}").GetProperty("get");
        Assert.True(query.GetProperty("security")[0].TryGetProperty("basic", out _));
        Assert.DoesNotContain(
            query.TryGetProperty("parameters", out var parameters) ? parameters.EnumerateArray() : Enumerable.Empty<JsonElement>(),
            p => p.GetProperty("in").GetString() == "header");
    }

    [Fact]
    public async Task Request_schemas_describe_what_the_api_accepts_so_swagger_builds_a_valid_example_body()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", cancellationToken));
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");

        // Enums are strings, with no null value.
        var journey = schemas.GetProperty("RegistrationJourney");
        Assert.Equal("string", journey.GetProperty("type").GetString());
        Assert.DoesNotContain(journey.GetProperty("enum").EnumerateArray(), value => value.ValueKind == JsonValueKind.Null);

        // A required request field points straight at the enum. A "null or enum" choice makes Swagger UI
        // generate a body the API can't read.
        var start = schemas.GetProperty("StartRegistrationRequest").GetProperty("properties");
        Assert.Equal("#/components/schemas/RegistrationJourney", start.GetProperty("journey").GetProperty("$ref").GetString());
        Assert.Equal("integer", start.GetProperty("registrationYear").GetProperty("type").GetString());

        // Optional request fields, and nullable response fields, can still be null.
        Assert.Contains(start.GetProperty("complianceSchemeId").GetProperty("type").EnumerateArray(), t => t.GetString() == "null");
        var view = schemas.GetProperty("RegistrationSubmissionView").GetProperty("properties");
        Assert.Contains(view.GetProperty("registrationReferenceNumber").GetProperty("type").EnumerateArray(), t => t.GetString() == "null");
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
