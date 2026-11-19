using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using WasteSubmitRegPackaging.Fees.Application.Queries.CalculatePackagingResubmissionFee;
using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.SharedKernel.Messaging;

namespace WasteSubmitRegPackagingApi.Test.Modules.Fees;

/// <summary>
/// The real host with the fee handler swapped for a substitute, so no database is needed. The database side
/// is covered by FeesSchemaTest.
/// </summary>
public class FeesEndpointsTest
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IQueryHandler<CalculatePackagingResubmissionFeeQuery, ResubmissionFee> _handler =
        Substitute.For<IQueryHandler<CalculatePackagingResubmissionFeeQuery, ResubmissionFee>>();

    [Fact]
    public async Task A_fee_request_with_a_bad_shape_is_a_400_validation_problem()
    {
        using var client = Client();

        var response = await client.PostAsJsonAsync("/fees/packaging-resubmission", new { memberCount = -1 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Submitter", out _), errors.ToString());
        Assert.True(errors.TryGetProperty("Regulator", out _), errors.ToString());
        Assert.True(errors.TryGetProperty("ResubmissionDate", out _), errors.ToString());
        Assert.True(errors.TryGetProperty("MemberCount", out _), errors.ToString());
    }

    [Fact]
    public async Task A_fee_request_passes_the_codes_through_and_returns_the_fee_in_pence()
    {
        _handler.HandleAsync(Arg.Any<CalculatePackagingResubmissionFeeQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ResubmissionFee([new FeeLine(26000028, 6, "Compliance Scheme re-submitting a report", 51200, 3, 153600)],
                153600, 1000, 152600, 3));
        using var client = Client();

        var response = await client.PostAsJsonAsync("/fees/packaging-resubmission", new
        {
            submitter = "ComplianceScheme",
            regulator = "GB-WLS",
            resubmissionDate = "2026-03-01T10:00:00Z",
            memberCount = 3,
            previousPayments = 1000
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(152600, body.RootElement.GetProperty("outstandingPayment").GetDecimal());
        Assert.Equal(3, body.RootElement.GetProperty("lines")[0].GetProperty("quantity").GetInt32());

        await _handler.Received(1).HandleAsync(
            Arg.Is<CalculatePackagingResubmissionFeeQuery>(q =>
                q.Input == new PackagingResubmissionFeeInput(
                    Submitter.ComplianceScheme, RegulatorNation.Wales, new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero), 3, 1000)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_fee_needs_no_user_headers_only_the_read_scope()
    {
        _handler.HandleAsync(Arg.Any<CalculatePackagingResubmissionFeeQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ResubmissionFee([], 71400, 0, 71400, 0));
        using var client = Client();

        var response = await client.PostAsJsonAsync("/fees/packaging-resubmission",
            new { submitter = "Producer", regulator = "GB-ENG", resubmissionDate = "2025-06-01T00:00:00Z" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Without_credentials_it_is_a_401()
    {
        await using var factory = new AclTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/submission-periods/packaging", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient Client()
    {
        var factory = new AclTestFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IQueryHandler<CalculatePackagingResubmissionFeeQuery, ResubmissionFee>>();
            services.AddSingleton(_handler);
        }));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);
        return client;
    }
}
