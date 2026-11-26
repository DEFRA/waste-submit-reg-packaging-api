using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WasteSubmitRegPackagingApi.Test.ServiceTests.SubmissionPeriods;

/// <summary>
/// The submission periods endpoints called over HTTP through the real host, handlers and SQL, on the periods the
/// changelog seeds. Only the clock is replaced, because what a submitter can see depends on the date. Every rule
/// for who sees what, and when, is in SubmissionPeriodsDatabaseTest. Needs Docker; skips without it.
/// </summary>
public sealed class SubmissionPeriodsEndpointsTest(ChangelogDatabase database) : IClassFixture<ChangelogDatabase>
{
    // 15 December 2026: everything up to July to December 2026 is visible; that one isn't open until 1 January 2027.
    private static readonly DateTimeOffset s_midDecember2026 = new(2026, 12, 15, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_packaging_submission_period_is_returned_with_its_data_periods()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.GetAsync("/submission-periods/packaging", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var period = body.RootElement[0];
        Assert.Equal("January to June 2023", period.GetProperty("name").GetString());
        Assert.Equal(2023, period.GetProperty("year").GetInt32());
        Assert.Equal(2024, period.GetProperty("registrationYear").GetInt32());
        Assert.NotEqual(0, period.GetProperty("dataPeriods").GetArrayLength());
    }

    [Fact]
    public async Task Without_a_submitter_type_it_is_a_400_validation_problem()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.GetAsync("/submission-periods/packaging/for-submitter-type", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("SubmitterType", out _), problem.RootElement.ToString());
    }

    [Theory]
    [InlineData("submitterType=Regulator")]
    // A number binds to the enum, so it has to be checked against the enum's values.
    [InlineData("submitterType=7")]
    [InlineData("submitterType=DirectProducer&producerSize=7")]
    public async Task An_unknown_submitter_type_or_producer_size_is_a_400(string query)
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.GetAsync($"/submission-periods/packaging/for-submitter-type?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_small_producer_gets_the_periods_that_accept_P0_as_tiles()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.GetAsync(
            "/submission-periods/packaging/for-submitter-type?submitterType=DirectProducer&producerSize=Small", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(
            ["July to December 2026", "July to December 2025", "July to December 2024"],
            body.RootElement.EnumerateArray().Select(tile => tile.GetProperty("name").GetString()));

        var newest = body.RootElement[0];
        Assert.False(newest.GetProperty("isOpen").GetBoolean());
        var dataPeriod = Assert.Single(newest.GetProperty("dataPeriods").EnumerateArray());
        Assert.Equal("2026-P0", dataPeriod.GetProperty("code").GetString());
        Assert.Equal("P0", dataPeriod.GetProperty("shortCode").GetString());
        Assert.Equal("Small", dataPeriod.GetProperty("producerSize").GetString());
        Assert.Equal("2026-01-01", dataPeriod.GetProperty("startDate").GetString());
    }

    [Fact]
    public async Task One_period_is_returned_as_an_object()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.GetAsync(
            "/submission-periods/packaging/July%20to%20December%202026/for-submitter-type?submitterType=ComplianceScheme", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("July to December 2026", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("2027-01-01T00:00:00", body.RootElement.GetProperty("openingDate").GetString());
        Assert.Equal(
            ["P0", "H2"],
            body.RootElement.GetProperty("dataPeriods").EnumerateArray().Select(d => d.GetProperty("shortCode").GetString()));
    }

    [Fact]
    public async Task A_period_the_submitter_cannot_see_yet_is_a_404()
    {
        // January to June 2027 isn't visible until June 2027.
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.GetAsync(
            "/submission-periods/packaging/January%20to%20June%202027/for-submitter-type?submitterType=DirectProducer", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_success_may_be_cached_privately_for_5_minutes()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.GetAsync("/submission-periods/packaging/for-submitter-type?submitterType=DirectProducer", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromMinutes(5), response.Headers.CacheControl?.MaxAge);
    }

    [Fact]
    public async Task An_error_is_not_cached()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var factory = App();
        using var client = Client(factory);

        var response = await client.GetAsync("/submission-periods/packaging/for-submitter-type", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.CacheControl?.MaxAge);
    }

    private WebApplicationFactory<Program> App() =>
        new AclTestFactory(PostgresSettings.For(database.ConnectionDetails)).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(s_midDecember2026));
            }));

    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AclTestFactory.Basic(AclTestFactory.ClientId, AclTestFactory.Secret);
        return client;
    }
}
