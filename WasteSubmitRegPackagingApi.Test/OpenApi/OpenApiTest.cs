using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using VerifyXunit;

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
    public async Task OpenApi_document_describes_the_user_headers_so_swagger_can_call_the_api()
    {
        // The endpoints that need a user are samples for now, so this reads the unpublished document.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(UnpublishedDocument, cancellationToken));
        var root = json.RootElement;

        var decision = root.GetProperty("paths").GetProperty("/regulator/registrations/{registrationSubmissionId}/decisions").GetProperty("post");
        Assert.Equal(["X-User-Id", "X-User-Type"], HeaderNames(decision));

        // A producer also says which organisation they act for.
        var submit = root.GetProperty("paths").GetProperty("/registrations/{registrationSubmissionId}/application-submission").GetProperty("post");
        Assert.Equal(["X-User-Id", "X-User-Type", "X-Organisation-Id"], HeaderNames(submit));

        // Queries need no user.
        var query = root.GetProperty("paths").GetProperty("/regulator/registrations/{registrationSubmissionId}").GetProperty("get");
        Assert.DoesNotContain(
            query.TryGetProperty("parameters", out var parameters) ? parameters.EnumerateArray() : Enumerable.Empty<JsonElement>(),
            p => p.GetProperty("in").GetString() == "header");
    }

    [Fact]
    public async Task The_v1_document_frontends_generate_from_has_no_sample_endpoints()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(V1Document, cancellationToken));
        var paths = json.RootElement.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToList();

        Assert.DoesNotContain(paths, IsSamplePath);
        Assert.Contains("/submission-periods/packaging/for-submitter-type", paths);
    }

    [Fact]
    public async Task The_fee_and_registration_submission_periods_endpoints_are_unpublished()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var v1 = JsonDocument.Parse(await client.GetStringAsync(V1Document, cancellationToken));
        using var unpublished = JsonDocument.Parse(await client.GetStringAsync(UnpublishedDocument, cancellationToken));
        string[] endpoints = ["/fees/packaging-resubmission", "/submission-periods/registration"];

        Assert.All(endpoints, path =>
        {
            Assert.False(v1.RootElement.GetProperty("paths").TryGetProperty(path, out _));
            Assert.True(unpublished.RootElement.GetProperty("paths").TryGetProperty(path, out _));
        });
    }

    [Fact]
    public async Task The_unpublished_document_has_the_registration_regulator_and_packaging_submission_samples()
    {
        // They show the structure and will be developed in due course, so callers must not build against them yet.
        // The unpublished document can hold other unpublished endpoints too, so this looks at the sample paths only.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(UnpublishedDocument, cancellationToken));
        var operations = json.RootElement.GetProperty("paths").EnumerateObject()
            .Where(path => IsSamplePath(path.Name))
            .SelectMany(path => path.Value.EnumerateObject())
            .ToList();

        Assert.Equal(7, operations.Count);
        Assert.All(operations, operation =>
        {
            Assert.EndsWith("(sample)", operation.Value.GetProperty("summary").GetString());
            Assert.StartsWith("SAMPLE:", operation.Value.GetProperty("description").GetString());
            Assert.All(operation.Value.GetProperty("tags").EnumerateArray(), tag => Assert.EndsWith("(sample)", tag.GetString()));
        });
    }

    [Fact]
    public async Task The_manage_obligations_endpoints_are_not_marked_as_samples()
    {
        // These are real: Manage Obligations calls them today.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(V1Document, cancellationToken));

        var operations = json.RootElement.GetProperty("paths").EnumerateObject()
            .Where(path => path.Name.StartsWith("/packaging/{packagingYear}", StringComparison.Ordinal))
            .SelectMany(path => path.Value.EnumerateObject())
            .ToList();

        Assert.Equal(2, operations.Count);
        Assert.All(operations, operation =>
        {
            Assert.DoesNotContain("sample", operation.Value.GetProperty("summary").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.All(operation.Value.GetProperty("tags").EnumerateArray(),
                tag => Assert.DoesNotContain("sample", tag.GetString(), StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task Each_submitter_type_keeps_its_own_values()
    {
        // Two C# enums are called SubmitterType. They once shared a schema, so Swagger offered Manage Obligations'
        // DirectRegistrant to the submission periods endpoint, which then failed to bind it.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(V1Document, cancellationToken));
        var root = json.RootElement;

        var parameter = Assert.Single(
            root.GetProperty("paths").GetProperty("/submission-periods/packaging/for-submitter-type").GetProperty("get")
                .GetProperty("parameters").EnumerateArray(),
            p => p.GetProperty("name").GetString() == "submitterType");

        Assert.Equal(["DirectProducer", "ComplianceScheme"], EnumValues(root, parameter.GetProperty("schema")));
        Assert.Equal(["DirectRegistrant", "ComplianceScheme"],
            EnumValues(root, root.GetProperty("components").GetProperty("schemas").GetProperty("ApprovedSubmission")
                .GetProperty("properties").GetProperty("submitterType")));
    }

    private static List<string?> EnumValues(JsonElement root, JsonElement schema)
    {
        var id = schema.GetProperty("$ref").GetString()!.Split('/')[^1];
        return root.GetProperty("components").GetProperty("schemas").GetProperty(id)
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToList();
    }

    /// <summary>
    /// The v1 document is the contract frontends generate clients from. Any change to it shows up here, and in the
    /// pull request as a change to the .verified.json file. Accept an intended change by replacing the .verified.json
    /// with the .received.json that the failing run writes.
    /// </summary>
    [Fact]
    public async Task The_v1_document_matches_the_approved_contract()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var document = await client.GetStringAsync(V1Document, cancellationToken);

        // The example GUIDs and dates are part of the contract, so they aren't scrubbed.
        await Verifier.VerifyJson(document).DontScrubGuids().DontScrubDateTimes();
    }

    private const string V1Document = "/openapi/v1.json";
    private const string UnpublishedDocument = "/openapi/unpublished.json";

    private static bool IsSamplePath(string path) =>
        path.StartsWith("/registrations", StringComparison.Ordinal)
        || path.StartsWith("/regulator/", StringComparison.Ordinal)
        || path.StartsWith("/packaging/submissions", StringComparison.Ordinal);

    private static List<string?> HeaderNames(JsonElement operation) =>
        operation.GetProperty("parameters").EnumerateArray()
            .Where(p => p.GetProperty("in").GetString() == "header")
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

    [Fact]
    public async Task Request_schemas_describe_what_the_api_accepts_so_swagger_builds_a_valid_example_body()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(UnpublishedDocument, cancellationToken));
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
        Assert.Contains(view.GetProperty("complianceSchemeId").GetProperty("type").EnumerateArray(), t => t.GetString() == "null");
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

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(V1Document, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(UnpublishedDocument, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/index.html", cancellationToken)).StatusCode);
    }
}
