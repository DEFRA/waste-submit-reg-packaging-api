using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.Fees;

/// <summary>
/// The fee endpoint called over HTTP through the real host and handler, on the fee rates the changelog seeds.
/// The parity checks for every rate are in FeesDatabaseTest. Needs Docker; skips without it.
/// </summary>
public sealed class FeesEndpointsTest(ChangelogDatabase database) : IClassFixture<ChangelogDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_fee_request_with_a_bad_shape_is_a_400_validation_problem()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

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
    public async Task An_enum_sent_as_a_number_that_is_not_one_of_its_values_is_a_400()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.PostAsJsonAsync("/fees/packaging-resubmission", new
        {
            submitter = 5,
            regulator = 9,
            resubmissionDate = "2026-03-01T10:00:00Z"
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Submitter", out _), errors.ToString());
        Assert.True(errors.TryGetProperty("Regulator", out _), errors.ToString());
    }

    [Fact]
    public async Task A_compliance_scheme_gets_the_seeded_rate_for_each_member_less_what_it_has_paid()
    {
        // The Welsh compliance scheme rate from 1 January 2026 is 51200 pence a member.
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.PostAsJsonAsync("/fees/packaging-resubmission", new
        {
            submitter = "ComplianceScheme",
            regulator = "GB-WLS",
            resubmissionDate = "2026-01-01T00:00:00Z",
            memberCount = 3,
            previousPayments = 1000
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(153600, body.RootElement.GetProperty("totalResubmissionFee").GetDecimal());
        Assert.Equal(152600, body.RootElement.GetProperty("outstandingPayment").GetDecimal());
        var line = Assert.Single(body.RootElement.GetProperty("lines").EnumerateArray());
        Assert.Equal(26000030, line.GetProperty("feeRateId").GetInt32());
        Assert.Equal(3, line.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task A_fee_needs_no_user_headers_only_the_read_scope()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.PostAsJsonAsync("/fees/packaging-resubmission",
            new { submitter = "Producer", regulator = "GB-ENG", resubmissionDate = "2025-06-01T12:00:00Z" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(71400, body.RootElement.GetProperty("totalResubmissionFee").GetDecimal());
    }

    private AclTestFactory App() => new(PostgresSettings.For(database.ConnectionDetails));

    private static HttpClient Client(AclTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);
        return client;
    }
}
