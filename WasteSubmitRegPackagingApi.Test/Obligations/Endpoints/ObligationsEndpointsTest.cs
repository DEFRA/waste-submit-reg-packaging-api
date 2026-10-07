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
    public async Task Get_returns_a_direct_registrant_as_a_single_item()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/obligations/organisations/{DirectRegistrantId}/approved-submissions?packagingYear=2024", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;

        Assert.Equal(new[] { "items" }, root.EnumerateObject().Select(property => property.Name));

        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(DirectRegistrantId, item.GetProperty("organisationId").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("joinerCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("joinerDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("leaverCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("leaverDate").ValueKind);

        var materials = item.GetProperty("materials").EnumerateArray().ToList();
        Assert.Equal(3, materials.Count);
        Assert.Equal("PL", materials[0].GetProperty("materialCode").GetString());
        Assert.Equal(412350, materials[0].GetProperty("weight").GetInt64());
    }

    [Fact]
    public async Task Get_returns_compliance_scheme_members_with_joiner_and_leaver_codes_as_numbers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/obligations/organisations/{ComplianceSchemeId}/approved-submissions?packagingYear=2024", cancellationToken);

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
    public async Task Get_returns_not_found_for_an_unknown_organisation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/obligations/organisations/{Guid.NewGuid()}/approved-submissions?packagingYear=2024", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Approved submissions not found", problem.Title);
    }

    [Fact]
    public async Task Get_returns_not_found_for_a_year_with_no_data()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/obligations/organisations/{DirectRegistrantId}/approved-submissions?packagingYear=2023", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_bad_request_when_packaging_year_is_missing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/obligations/organisations/{DirectRegistrantId}/approved-submissions", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}