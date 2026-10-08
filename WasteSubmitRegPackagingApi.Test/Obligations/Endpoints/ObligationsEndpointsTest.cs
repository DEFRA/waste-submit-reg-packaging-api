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

    [Fact]
    public async Task Filtered_endpoint_returns_a_direct_registrant_as_a_single_item()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/2024/organisation/{DirectRegistrantId}/aggregated-submission", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;

        Assert.Equal(new[] { "items" }, root.EnumerateObject().Select(property => property.Name));

        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(DirectRegistrantId, item.GetProperty("organisationId").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("joinerCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("leaverCode").ValueKind);

        var materials = item.GetProperty("materials").EnumerateArray().ToList();
        Assert.Equal(3, materials.Count);
        Assert.Equal("PL", materials[0].GetProperty("materialCode").GetString());
        Assert.Equal(412.35m, materials[0].GetProperty("tonnes").GetDecimal());
    }

    [Fact]
    public async Task Filtered_endpoint_returns_compliance_scheme_members_with_codes_as_numbers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/2024/organisation/{ComplianceSchemeId}/aggregated-submission", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);

        var joiner = items[1];
        Assert.Equal(JsonValueKind.Number, joiner.GetProperty("joinerCode").ValueKind);
        Assert.Equal(3, joiner.GetProperty("joinerCode").GetInt32());
        Assert.Equal("2025-07-05", joiner.GetProperty("joinerDate").GetString());
        Assert.Equal(JsonValueKind.Null, joiner.GetProperty("leaverCode").ValueKind);

        var leaver = items[2];
        Assert.Equal(13, leaver.GetProperty("leaverCode").GetInt32());
        Assert.Equal("2025-09-30", leaver.GetProperty("leaverDate").GetString());
        Assert.Equal(JsonValueKind.Null, leaver.GetProperty("joinerCode").ValueKind);
    }

    [Fact]
    public async Task Filtered_endpoint_returns_not_found_for_an_unknown_organisation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/2024/organisation/{Guid.NewGuid()}/aggregated-submission", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Approved submissions not found", problem.Title);
    }

    [Fact]
    public async Task Filtered_endpoint_returns_not_found_for_a_year_with_no_data()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/packaging/2023/organisation/{DirectRegistrantId}/aggregated-submission", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unfiltered_endpoint_returns_every_approved_submission_grouped_by_submitter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/packaging/2024/aggregated-submissions", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;

        Assert.Equal(new[] { "approvedSubmissions" }, root.EnumerateObject().Select(property => property.Name));

        var submissions = root.GetProperty("approvedSubmissions").EnumerateArray().ToList();
        Assert.Equal(2, submissions.Count);

        var direct = submissions[0];
        Assert.Equal(DirectRegistrantId, direct.GetProperty("submitterId").GetString());
        Assert.Equal("DirectRegistrant", direct.GetProperty("submitterType").GetString());
        var directItem = Assert.Single(direct.GetProperty("aggregatedPackaging").EnumerateArray());
        Assert.Equal(DirectRegistrantId, directItem.GetProperty("organisationId").GetString());

        var scheme = submissions[1];
        Assert.Equal(ComplianceSchemeId, scheme.GetProperty("submitterId").GetString());
        Assert.Equal("ComplianceScheme", scheme.GetProperty("submitterType").GetString());
        Assert.Equal(3, scheme.GetProperty("aggregatedPackaging").GetArrayLength());
    }

    [Fact]
    public async Task Unfiltered_endpoint_returns_not_found_for_a_year_with_no_data()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/packaging/2023/aggregated-submissions", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Year_must_be_a_number()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/packaging/latest/aggregated-submissions", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}