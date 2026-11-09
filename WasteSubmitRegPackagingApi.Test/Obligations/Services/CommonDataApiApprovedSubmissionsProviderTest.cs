using System.Net;
using System.Text;
using WasteSubmitRegPackagingApi.Obligations.Models;
using WasteSubmitRegPackagingApi.Obligations.Services;

namespace WasteSubmitRegPackagingApi.Test.Obligations.Services;

public class CommonDataApiApprovedSubmissionsProviderTest
{
    private static (CommonDataApiApprovedSubmissionsProvider Provider, StubHandler Handler) CreateProvider(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new StubHandler(json, statusCode);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://common-data-api.test/api/submissions/") };

        return (new CommonDataApiApprovedSubmissionsProvider(httpClient), handler);
    }

    [Fact]
    public async Task Groups_rows_by_submitter_then_producer_and_sums_materials_per_code()
    {
        var (provider, _) = CreateProvider("""
            [
              { "submitterType": "DirectRegistrant", "submitterId": "11111111-1111-4111-8111-111111111111", "organisationId": "11111111-1111-4111-8111-111111111111", "packagingMaterial": "PL", "packagingMaterialWeightKg": 400000 },
              { "submitterType": "DirectRegistrant", "submitterId": "11111111-1111-4111-8111-111111111111", "organisationId": "11111111-1111-4111-8111-111111111111", "packagingMaterial": "PL", "packagingMaterialWeightKg": 12350 },
              { "submitterType": "DirectRegistrant", "submitterId": "11111111-1111-4111-8111-111111111111", "organisationId": "11111111-1111-4111-8111-111111111111", "packagingMaterial": "PC", "packagingMaterialWeightKg": 655120 }
            ]
            """);

        var result = await provider.GetProducerMaterialTotalsBySubmitterAsync(2024, TestContext.Current.CancellationToken);

        var submission = Assert.Single(result.Items);
        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111111111"), submission.SubmitterOrganisationId);
        Assert.Equal(SubmitterType.DirectRegistrant, submission.SubmitterType);

        var producer = Assert.Single(submission.Producers);
        Assert.Empty(producer.Events);
        Assert.Equal(
            new[] { ("PL", 412350L), ("PC", 655120L) },
            producer.Materials.Select(material => (material.MaterialCode, material.Kilograms)));
    }

    [Fact]
    public async Task Groups_producers_within_a_submitter_separately()
    {
        var (provider, _) = CreateProvider("""
            [
              { "submitterType": "ComplianceScheme", "submitterId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "organisationId": "22222222-2222-4222-8222-222222222222", "packagingMaterial": "PL", "packagingMaterialWeightKg": 100000 },
              { "submitterType": "ComplianceScheme", "submitterId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "organisationId": "33333333-3333-4333-8333-333333333333", "packagingMaterial": "GL", "packagingMaterialWeightKg": 64000 }
            ]
            """);

        var result = await provider.GetProducerMaterialTotalsBySubmitterAsync(2024, TestContext.Current.CancellationToken);

        var submission = Assert.Single(result.Items);
        Assert.Equal(2, submission.Producers.Count);
    }

    [Fact]
    public async Task Maps_a_leaver_row_to_a_single_leaver_event()
    {
        var (provider, _) = CreateProvider("""
            [
              { "submitterType": "ComplianceScheme", "submitterId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "organisationId": "44444444-4444-4444-8444-444444444444", "packagingMaterial": "GL", "packagingMaterialWeightKg": 64000, "leaverCode": "13", "leaverDate": "2025-09-30T00:00:00" }
            ]
            """);

        var result = await provider.GetProducerMaterialTotalsBySubmitterAsync(2024, TestContext.Current.CancellationToken);

        var producer = Assert.Single(Assert.Single(result.Items).Producers);
        var leaverJoinerEvent = Assert.Single(producer.Events);
        Assert.Equal(13, leaverJoinerEvent.Code);
        Assert.Equal(EventAction.Left, leaverJoinerEvent.Action);
        Assert.Equal(new DateOnly(2025, 9, 30), leaverJoinerEvent.Date);
    }

    [Fact]
    public async Task Maps_a_joiner_row_to_a_single_joiner_event()
    {
        var (provider, _) = CreateProvider("""
            [
              { "submitterType": "ComplianceScheme", "submitterId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "organisationId": "33333333-3333-4333-8333-333333333333", "packagingMaterial": "PL", "packagingMaterialWeightKg": 75000, "leaverCode": "03", "joinerDate": "2025-07-05T00:00:00" }
            ]
            """);

        var result = await provider.GetProducerMaterialTotalsBySubmitterAsync(2024, TestContext.Current.CancellationToken);

        var producer = Assert.Single(Assert.Single(result.Items).Producers);
        var joinerEvent = Assert.Single(producer.Events);
        Assert.Equal(3, joinerEvent.Code);
        Assert.Equal(EventAction.Joined, joinerEvent.Action);
        Assert.Equal(new DateOnly(2025, 7, 5), joinerEvent.Date);
    }

    [Fact]
    public async Task Drops_an_unmapped_leaver_code_such_as_18_rather_than_throwing()
    {
        var (provider, _) = CreateProvider("""
            [
              { "submitterType": "DirectRegistrant", "submitterId": "11111111-1111-4111-8111-111111111111", "organisationId": "11111111-1111-4111-8111-111111111111", "packagingMaterial": "PL", "packagingMaterialWeightKg": 1000, "leaverCode": "18", "leaverDate": "2025-01-01T00:00:00" }
            ]
            """);

        var result = await provider.GetProducerMaterialTotalsBySubmitterAsync(2024, TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(Assert.Single(result.Items).Producers).Events);
    }

    [Fact]
    public async Task No_content_response_returns_empty_items()
    {
        var (provider, _) = CreateProvider("", HttpStatusCode.NoContent);

        var result = await provider.GetProducerMaterialTotalsBySubmitterAsync(2024, TestContext.Current.CancellationToken);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Filtered_method_returns_empty_items_rather_than_null_for_an_unknown_submitter()
    {
        var (provider, _) = CreateProvider("[]");

        var result = await provider.GetProducerMaterialTotalsAsync(2024, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Filtered_method_returns_just_the_matching_submitters_producers()
    {
        var (provider, _) = CreateProvider("""
            [
              { "submitterType": "DirectRegistrant", "submitterId": "11111111-1111-4111-8111-111111111111", "organisationId": "11111111-1111-4111-8111-111111111111", "packagingMaterial": "PL", "packagingMaterialWeightKg": 1000 },
              { "submitterType": "ComplianceScheme", "submitterId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "organisationId": "22222222-2222-4222-8222-222222222222", "packagingMaterial": "GL", "packagingMaterialWeightKg": 2000 }
            ]
            """);

        var result = await provider.GetProducerMaterialTotalsAsync(2024, Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), TestContext.Current.CancellationToken);

        var producer = Assert.Single(result!.Items);
        Assert.Equal(Guid.Parse("22222222-2222-4222-8222-222222222222"), producer.ProducerOrganisationId);
    }

    [Fact]
    public async Task Requests_the_endpoint_for_the_given_year()
    {
        var (provider, handler) = CreateProvider("[]");

        await provider.GetProducerMaterialTotalsBySubmitterAsync(2024, TestContext.Current.CancellationToken);

        Assert.Equal("https://common-data-api.test/api/submissions/v1/pom/approved-submissions/2024", handler.LastRequest?.RequestUri?.ToString());
    }

    private sealed class StubHandler(string json, HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            var response = new HttpResponseMessage(statusCode);
            if (statusCode != HttpStatusCode.NoContent)
            {
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }
}
