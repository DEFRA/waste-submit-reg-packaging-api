using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WasteSubmitRegPackagingApi.Test.Obligations.Endpoints;

public class ObligationsEndpointsTest
{
    private const string DirectRegistrantId = "11111111-1111-4111-8111-111111111111";
    private const string ComplianceSchemeId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string NoSubmissionsId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";

    [Fact]
    public async Task Filtered_endpoint_returns_a_direct_registrant_as_a_single_item()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/2024/submitter-organisation/{DirectRegistrantId}/producer-material-totals", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;

        Assert.Equal(new[] { "items" }, root.EnumerateObject().Select(property => property.Name));

        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(DirectRegistrantId, item.GetProperty("producerOrganisationId").GetString());
        Assert.Equal(new[] { "producerOrganisationId", "events", "materials" }, item.EnumerateObject().Select(property => property.Name));
        Assert.Equal(0, item.GetProperty("events").GetArrayLength());

        var materials = item.GetProperty("materials").EnumerateArray().ToList();
        Assert.Equal(3, materials.Count);
        Assert.Equal("PL", materials[0].GetProperty("materialCode").GetString());
        Assert.Equal(412350L, materials[0].GetProperty("kilograms").GetInt64());
    }

    [Fact]
    public async Task Filtered_endpoint_returns_compliance_scheme_members_with_their_events()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/2024/submitter-organisation/{ComplianceSchemeId}/producer-material-totals", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(4, items.Count);

        Assert.Equal(0, items[0].GetProperty("events").GetArrayLength());

        var joined = Assert.Single(items[1].GetProperty("events").EnumerateArray());
        Assert.Equal(JsonValueKind.Number, joined.GetProperty("code").ValueKind);
        Assert.Equal((3, "Joined", "2025-07-05"), Describe(joined));

        var left = Assert.Single(items[2].GetProperty("events").EnumerateArray());
        Assert.Equal((13, "Left", "2025-09-30"), Describe(left));
    }

    [Fact]
    public async Task Filtered_endpoint_returns_every_event_for_a_member_that_joins_leaves_and_rejoins_oldest_first()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/2024/submitter-organisation/{ComplianceSchemeId}/producer-material-totals", cancellationToken);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var member = json.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("producerOrganisationId").GetString() == "55555555-5555-4555-8555-555555555555");

        Assert.Equal(
            new[] { (1, "Joined", "2025-03-03"), (13, "Left", "2025-07-14"), (1, "Joined", "2025-10-01") },
            member.GetProperty("events").EnumerateArray().Select(Describe));
    }

    [Fact]
    public async Task Filtered_endpoint_returns_not_found_for_an_unknown_organisation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/2024/submitter-organisation/{Guid.NewGuid()}/producer-material-totals", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Submitter organisation not found", problem.Title);
    }

    [Theory]
    [InlineData("2023", DirectRegistrantId)]
    [InlineData("2024", NoSubmissionsId)]
    public async Task Filtered_endpoint_returns_empty_items_for_a_known_organisation_with_no_approved_submission(string packagingYear, string submitterOrganisationId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/{packagingYear}/submitter-organisation/{submitterOrganisationId}/producer-material-totals", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Unfiltered_endpoint_returns_every_approved_submission_grouped_by_submitter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/packaging/2024/producer-material-totals-by-submitter", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;

        Assert.Equal(new[] { "items" }, root.EnumerateObject().Select(property => property.Name));

        var submissions = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, submissions.Count);

        var direct = submissions[0];
        Assert.Equal(DirectRegistrantId, direct.GetProperty("submitterOrganisationId").GetString());
        Assert.Equal("DirectRegistrant", direct.GetProperty("submitterType").GetString());
        var directItem = Assert.Single(direct.GetProperty("producers").EnumerateArray());
        Assert.Equal(DirectRegistrantId, directItem.GetProperty("producerOrganisationId").GetString());

        var scheme = submissions[1];
        Assert.Equal(ComplianceSchemeId, scheme.GetProperty("submitterOrganisationId").GetString());
        Assert.Equal("ComplianceScheme", scheme.GetProperty("submitterType").GetString());
        Assert.Equal(4, scheme.GetProperty("producers").GetArrayLength());
    }

    [Fact]
    public async Task Unfiltered_endpoint_returns_empty_items_for_a_year_with_no_data()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/packaging/2023/producer-material-totals-by-submitter", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Year_must_be_a_number()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/packaging/latest/producer-material-totals-by-submitter", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static (int Code, string? Action, string? Date) Describe(JsonElement leaverJoinerEvent) =>
        (leaverJoinerEvent.GetProperty("code").GetInt32(),
            leaverJoinerEvent.GetProperty("action").GetString(),
            leaverJoinerEvent.GetProperty("date").GetString());
}
