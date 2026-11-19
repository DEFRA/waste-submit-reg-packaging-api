using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetPackagingSubmissionPeriodsForSubmitter;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;

namespace WasteSubmitRegPackagingApi.Test.Modules.SubmissionPeriods;

/// <summary>
/// The real host with the for-submitter-type handler swapped for a substitute, so no database is needed. The SQL and the
/// filtering are covered by SubmissionPeriodsSchemaTest.
/// </summary>
public class SubmissionPeriodsEndpointsTest
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IQueryHandler<GetPackagingSubmissionPeriodsForSubmitterQuery, IReadOnlyList<PackagingSubmissionPeriodTile>> _handler =
        Substitute.For<IQueryHandler<GetPackagingSubmissionPeriodsForSubmitterQuery, IReadOnlyList<PackagingSubmissionPeriodTile>>>();

    private static readonly PackagingSubmissionPeriodTile s_tile = new(
        "July to December 2026", 2026, new DateTime(2027, 1, 1), new DateTime(2027, 4, 1, 23, 59, 0), false,
        [new PackagingDataPeriod("2026-P0", "P0", "January to December 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), ProducerSize.Small)]);

    [Fact]
    public async Task Without_a_submitter_type_it_is_a_400_validation_problem()
    {
        using var client = Client();

        var response = await client.GetAsync("/submission-periods/packaging/for-submitter-type", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("SubmitterType", out _), problem.RootElement.ToString());
    }

    [Fact]
    public async Task An_unknown_submitter_type_is_a_400()
    {
        using var client = Client();

        var response = await client.GetAsync("/submission-periods/packaging/for-submitter-type?submitterType=Regulator", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_submitter_is_passed_through_and_the_tiles_come_back_as_json()
    {
        _handler.HandleAsync(Arg.Any<GetPackagingSubmissionPeriodsForSubmitterQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<PackagingSubmissionPeriodTile>>.Success([s_tile]));
        using var client = Client();

        var response = await client.GetAsync(
            "/submission-periods/packaging/for-submitter-type?submitterType=DirectProducer&producerSize=Small", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var dataPeriod = body.RootElement[0].GetProperty("dataPeriods")[0];
        Assert.Equal("P0", dataPeriod.GetProperty("shortCode").GetString());
        Assert.Equal("Small", dataPeriod.GetProperty("producerSize").GetString());
        Assert.Equal("2026-01-01", dataPeriod.GetProperty("startDate").GetString());
        Assert.False(body.RootElement[0].GetProperty("isOpen").GetBoolean());

        await _handler.Received(1).HandleAsync(
            new GetPackagingSubmissionPeriodsForSubmitterQuery(SubmitterType.DirectProducer, ProducerSize.Small),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task One_period_is_returned_as_an_object()
    {
        _handler.HandleAsync(Arg.Any<GetPackagingSubmissionPeriodsForSubmitterQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<PackagingSubmissionPeriodTile>>.Success([s_tile]));
        using var client = Client();

        var response = await client.GetAsync(
            "/submission-periods/packaging/July%20to%20December%202026/for-submitter-type?submitterType=ComplianceScheme", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("July to December 2026", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("2027-01-01T00:00:00", body.RootElement.GetProperty("openingDate").GetString());
        await _handler.Received(1).HandleAsync(
            new GetPackagingSubmissionPeriodsForSubmitterQuery(SubmitterType.ComplianceScheme, null, "July to December 2026"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_period_the_submitter_cannot_see_is_a_404()
    {
        _handler.HandleAsync(Arg.Any<GetPackagingSubmissionPeriodsForSubmitterQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<PackagingSubmissionPeriodTile>>.Failure(
                Error.NotFound("submission_periods.packaging_submission_period_not_available", "Not available.")));
        using var client = Client();

        var response = await client.GetAsync(
            "/submission-periods/packaging/January%20to%20June%202027/for-submitter-type?submitterType=DirectProducer", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient Client()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IQueryHandler<GetPackagingSubmissionPeriodsForSubmitterQuery, IReadOnlyList<PackagingSubmissionPeriodTile>>>();
            services.AddSingleton(_handler);
        }));

        var client = factory.CreateClient();
        return client;
    }
}
