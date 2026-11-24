using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.SharedKernel.Results;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetPackagingSubmissionPeriods;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetPackagingSubmissionPeriodsForSubmitter;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetRegistrationSubmissionPeriods;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;
using WasteSubmitRegPackaging.SubmissionPeriods.Infrastructure;

namespace WasteSubmitRegPackagingApi.Test.Modules.SubmissionPeriods;

/// <summary>
/// The submission periods the changelog seeds, read through the real handlers on Postgres: they must match
/// today's packaging config and the payment service's registration submission periods. This also checks the SQL
/// against the real changelog. Needs Docker; skips without it.
/// </summary>
public sealed class SubmissionPeriodsSchemaTest(ChangelogDatabase database) : IClassFixture<ChangelogDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 15 December 2026: everything up to July to December 2026 is visible; that one isn't open until 1 January 2027.
    private static readonly DateTimeOffset s_midDecember2026 = new(2026, 12, 15, 12, 0, 0, TimeSpan.Zero);

    // ---- Registration ----

    [Fact]
    public async Task The_registration_submission_periods_keep_the_payment_services_ids_and_values()
    {
        // Registration data moves here from epr-payment-service, and its RegistrationSubmissionData.SubmissionPeriodId
        // is one of these ids. These are the payment service's [Lookup].[SubmissionPeriod] rows (SubmissionPeriodDataSeed).
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var periods = (await new GetRegistrationSubmissionPeriodsHandler(context)
            .HandleAsync(new GetRegistrationSubmissionPeriodsQuery(), Ct)).Value;

        Assert.Equal(
            [
                new RegistrationSubmissionPeriod(1, "Cso", 2025, Utc(2024, 7, 1), Utc(2025, 4, 2), Utc(2026, 2, 1)),
                new RegistrationSubmissionPeriod(2, "Direct", 2025, Utc(2024, 7, 1), Utc(2025, 4, 2), Utc(2026, 1, 1)),
                new RegistrationSubmissionPeriod(3, "CsoLargeProducer", 2026, Utc(2025, 7, 1), Utc(2025, 10, 2), Utc(2027, 1, 29)),
                new RegistrationSubmissionPeriod(4, "CsoSmallProducer", 2026, Utc(2026, 1, 1), Utc(2026, 4, 2), Utc(2027, 1, 29)),
                new RegistrationSubmissionPeriod(5, "DirectLargeProducer", 2026, Utc(2025, 7, 1), Utc(2025, 10, 2), Utc(2027, 1, 1)),
                new RegistrationSubmissionPeriod(6, "DirectSmallProducer", 2026, Utc(2026, 1, 1), Utc(2026, 4, 2), Utc(2027, 1, 1)),
                new RegistrationSubmissionPeriod(7, "CsoLargeProducer", 2027, Utc(2026, 7, 1), Utc(2026, 10, 2), Utc(2028, 1, 29)),
                new RegistrationSubmissionPeriod(8, "CsoSmallProducer", 2027, Utc(2027, 1, 1), Utc(2027, 4, 2), Utc(2028, 1, 29)),
                new RegistrationSubmissionPeriod(9, "DirectLargeProducer", 2027, Utc(2026, 7, 1), Utc(2026, 10, 2), Utc(2028, 1, 1)),
                new RegistrationSubmissionPeriod(10, "DirectSmallProducer", 2027, Utc(2027, 1, 1), Utc(2027, 4, 2), Utc(2028, 1, 1)),
                new RegistrationSubmissionPeriod(11, "Cso", 2024, Utc(2023, 7, 1), Utc(2024, 4, 2), Utc(2025, 2, 1)),
                new RegistrationSubmissionPeriod(12, "Direct", 2024, Utc(2023, 7, 1), Utc(2024, 4, 2), Utc(2025, 1, 1)),
            ],
            periods);
    }

    [Fact]
    public async Task The_registration_submission_periods_can_be_read_for_one_year()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var periods = (await new GetRegistrationSubmissionPeriodsHandler(context)
            .HandleAsync(new GetRegistrationSubmissionPeriodsQuery(2026), Ct)).Value;

        Assert.Equal([3, 4, 5, 6], periods.Select(p => p.Id));
    }

    // ---- Packaging ----

    [Fact]
    public async Task Each_packaging_submission_period_is_for_the_following_registration_year()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var periods = (await new GetPackagingSubmissionPeriodsHandler(context)
            .HandleAsync(new GetPackagingSubmissionPeriodsQuery(), Ct)).Value;

        Assert.Equal(10, periods.Count);
        Assert.Equal("January to June 2023", periods[0].Name);
        Assert.All(periods, p => Assert.NotEmpty(p.DataPeriods));
        Assert.All(periods, p => Assert.Equal(p.Year + 1, p.RegistrationYear));
    }

    [Fact]
    public async Task A_period_code_finds_its_submission_period_and_dates()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var period = Assert.Single((await new GetPackagingSubmissionPeriodsHandler(context)
            .HandleAsync(new GetPackagingSubmissionPeriodsQuery("2025-P0"), Ct)).Value);

        Assert.Equal("July to December 2025", period.Name);
        // P0 is the whole year, reported by small producers in the July to December submission period.
        Assert.Equal(
            new PackagingDataPeriod("2025-P0", "P0", "January to December 2025", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), ProducerSize.Small),
            Assert.Single(period.DataPeriods));
        Assert.Equal(new DateTime(2026, 1, 1), period.OpeningDate);
        Assert.Equal(new DateTime(2026, 4, 1, 23, 59, 0), period.DeadlineDate);
        Assert.Equal(2026, period.RegistrationYear);
    }

    [Fact]
    public async Task The_part_year_data_periods_match_the_calculators_dates()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var period = Assert.Single((await new GetPackagingSubmissionPeriodsHandler(context)
            .HandleAsync(new GetPackagingSubmissionPeriodsQuery(), Ct)).Value, p => p.Name == "January to June 2024");

        Assert.Equal(
            [("2024-P1", new DateOnly(2024, 1, 1)), ("2024-P2", new DateOnly(2024, 4, 1)), ("2024-P3", new DateOnly(2024, 5, 1))],
            period.DataPeriods.Select(d => (d.Code, d.StartDate)));
        Assert.All(period.DataPeriods, d => Assert.Equal(new DateOnly(2024, 6, 30), d.EndDate));
    }

    [Fact]
    public async Task Only_P0_is_for_small_producers()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var dataPeriods = (await new GetPackagingSubmissionPeriodsHandler(context)
            .HandleAsync(new GetPackagingSubmissionPeriodsQuery(), Ct)).Value.SelectMany(p => p.DataPeriods).ToList();

        Assert.Equal(17, dataPeriods.Count);
        Assert.All(dataPeriods, d => Assert.Equal(d.ShortCode == "P0" ? ProducerSize.Small : ProducerSize.Large, d.ProducerSize));
    }

    [Fact]
    public async Task An_unknown_period_code_finds_nothing()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var periods = (await new GetPackagingSubmissionPeriodsHandler(context)
            .HandleAsync(new GetPackagingSubmissionPeriodsQuery("1999-H1"), Ct)).Value;

        Assert.Empty(periods);
    }

    // ---- Packaging tiles for one submitter ----

    [Fact]
    public async Task A_compliance_scheme_sees_every_visible_period_with_every_data_period_newest_year_first()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");

        var tiles = await TilesAsync(SubmitterType.ComplianceScheme, null, s_midDecember2026);

        Assert.Equal(
            [
                "January to June 2026", "July to December 2026", "January to June 2025", "July to December 2025",
                "January to June 2024", "July to December 2024", "January to June 2023", "July to December 2023",
            ],
            tiles.Select(t => t.Name));
        var julyToDecember2026 = tiles[1];
        Assert.False(julyToDecember2026.IsOpen);
        Assert.Equal(["P0", "H2"], julyToDecember2026.DataPeriods.Select(d => d.ShortCode));
        Assert.True(tiles[0].IsOpen);
    }

    [Fact]
    public async Task A_small_producer_sees_only_the_periods_that_accept_P0()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");

        var tiles = await TilesAsync(SubmitterType.DirectProducer, ProducerSize.Small, s_midDecember2026);

        Assert.Equal(["July to December 2026", "July to December 2025", "July to December 2024"], tiles.Select(t => t.Name));
        Assert.All(tiles, t => Assert.Equal("P0", Assert.Single(t.DataPeriods).ShortCode));
    }

    [Fact]
    public async Task A_large_producer_sees_every_period_without_P0()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");

        var tiles = await TilesAsync(SubmitterType.DirectProducer, ProducerSize.Large, s_midDecember2026);

        Assert.Equal(8, tiles.Count);
        Assert.DoesNotContain(tiles.SelectMany(t => t.DataPeriods), d => d.ProducerSize == ProducerSize.Small);
        Assert.Equal("H2", Assert.Single(tiles[1].DataPeriods).ShortCode);
    }

    [Fact]
    public async Task A_direct_producer_of_unknown_size_sees_what_a_compliance_scheme_sees()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");

        var producer = await TilesAsync(SubmitterType.DirectProducer, null, s_midDecember2026);
        var scheme = await TilesAsync(SubmitterType.ComplianceScheme, null, s_midDecember2026);

        Assert.Equal(scheme, producer, TileComparer.Instance);
    }

    [Theory]
    // July to December 2026 is visible from 1 December 2026, 00:00 UK time (GMT, so the same as UTC).
    [InlineData("2026-11-30T23:59:00Z", false)]
    [InlineData("2026-12-01T00:00:00Z", true)]
    public async Task A_period_appears_from_its_visible_from_date(string utcNow, bool visible)
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");

        var tiles = await TilesAsync(SubmitterType.ComplianceScheme, null, DateTimeOffset.Parse(utcNow));

        Assert.Equal(visible, tiles.Any(t => t.Name == "July to December 2026"));
    }

    [Theory]
    // January to June 2026 opens on 1 April 2026, 00:00 UK time, which is British Summer Time: 31 March, 23:00 UTC.
    [InlineData("2026-03-31T22:59:00Z", false)]
    [InlineData("2026-03-31T23:00:00Z", true)]
    public async Task Times_are_compared_in_UK_time(string utcNow, bool shown)
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");

        var tiles = await TilesAsync(SubmitterType.ComplianceScheme, null, DateTimeOffset.Parse(utcNow));

        var januaryToJune2026 = tiles.SingleOrDefault(t => t.Name == "January to June 2026");
        Assert.Equal(shown, januaryToJune2026 is { IsOpen: true });
    }

    [Fact]
    public async Task One_period_is_returned_for_the_page_after_a_tile_is_chosen()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var tile = Assert.Single((await Handler(context, s_midDecember2026).HandleAsync(
            new GetPackagingSubmissionPeriodsForSubmitterQuery(SubmitterType.DirectProducer, ProducerSize.Small, "July to December 2026"), Ct)).Value);

        var p0 = Assert.Single(tile.DataPeriods);
        Assert.Equal(("2026-P0", "P0", "January to December 2026"), (p0.Code, p0.ShortCode, p0.Description));
    }

    [Theory]
    [InlineData("January to June 2027", null)]                  // not visible until June 2027
    [InlineData("January to June 2026", ProducerSize.Small)]    // nothing for a small producer
    [InlineData("Some other period", null)]                     // doesn't exist
    public async Task A_period_the_submitter_cannot_see_is_not_found(string submissionPeriod, ProducerSize? size)
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var result = await Handler(context, s_midDecember2026).HandleAsync(
            new GetPackagingSubmissionPeriodsForSubmitterQuery(SubmitterType.DirectProducer, size, submissionPeriod), Ct);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task A_producer_size_for_a_compliance_scheme_is_rejected()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var result = await Handler(context, s_midDecember2026).HandleAsync(
            new GetPackagingSubmissionPeriodsForSubmitterQuery(SubmitterType.ComplianceScheme, ProducerSize.Large), Ct);

        Assert.Equal("submission_periods.producer_size_not_for_compliance_schemes", result.Error.Code);
    }

    [Fact]
    public async Task Submission_periods_cannot_be_changed_by_the_app()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
    }

    private async Task<IReadOnlyList<PackagingSubmissionPeriodTile>> TilesAsync(SubmitterType submitter, ProducerSize? size, DateTimeOffset utcNow)
    {
        await using var context = CreateContext();
        return (await Handler(context, utcNow).HandleAsync(new GetPackagingSubmissionPeriodsForSubmitterQuery(submitter, size), Ct)).Value;
    }

    private static GetPackagingSubmissionPeriodsForSubmitterHandler Handler(SubmissionPeriodsDbContext context, DateTimeOffset utcNow) =>
        new(context, new FixedTimeProvider(utcNow));

    private static DateTimeOffset Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);

    private SubmissionPeriodsDbContext CreateContext() => new(database.Options<SubmissionPeriodsDbContext>());

    /// <summary>Records compare lists by reference, so compare tiles by their values.</summary>
    private sealed class TileComparer : IEqualityComparer<PackagingSubmissionPeriodTile>
    {
        public static readonly TileComparer Instance = new();

        public bool Equals(PackagingSubmissionPeriodTile? x, PackagingSubmissionPeriodTile? y) =>
            x is not null && y is not null
            && (x.Name, x.Year, x.OpeningDate, x.DeadlineDate, x.IsOpen) == (y.Name, y.Year, y.OpeningDate, y.DeadlineDate, y.IsOpen)
            && x.DataPeriods.SequenceEqual(y.DataPeriods);

        public int GetHashCode(PackagingSubmissionPeriodTile obj) => obj.Name.GetHashCode(StringComparison.Ordinal);
    }
}
