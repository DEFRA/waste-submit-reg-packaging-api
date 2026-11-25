using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using VerifyXunit;

namespace WasteSubmitRegPackagingApi.Test.ContractTests;

/// <summary>
/// The v1 document is pinned by the snapshot, so a test here is only for a rule the snapshot can't state: one that
/// must hold for endpoints published later, or a past fault a reviewer could miss in a snapshot diff. The unpublished
/// document isn't tested: it describes endpoints that will change.
/// </summary>
public class OpenApiTest
{
    private const string V1Document = "/openapi/v1.json";
    private const string UnpublishedDocument = "/openapi/unpublished.json";

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

    [Fact]
    public async Task Every_secured_v1_endpoint_documents_401_and_403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(V1Document, cancellationToken));
        var operations = json.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .ToList();

        Assert.NotEmpty(operations);
        Assert.All(operations, operation =>
        {
            Assert.True(operation.Value.GetProperty("responses").TryGetProperty("401", out _));
            Assert.True(operation.Value.GetProperty("responses").TryGetProperty("403", out _));
        });
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
