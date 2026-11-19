using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Fees.Application.Queries.CalculatePackagingResubmissionFee;
using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.Fees.Infrastructure;

namespace WasteSubmitRegPackagingApi.Test.Modules.Fees;

/// <summary>
/// The fee rates the changelog seeds, read through the real handler on Postgres. These are the parity checks
/// against epr-payment-service's rates. Needs Docker; skips without it.
/// </summary>
public sealed class FeesSchemaTest(ChangelogDatabase database) : IClassFixture<ChangelogDatabase>
{
    [Theory]
    [InlineData(Submitter.Producer, RegulatorNation.England, "2025-06-01T12:00:00Z", 0, 71400, 45)]
    [InlineData(Submitter.Producer, RegulatorNation.Scotland, "2026-03-01T00:00:00Z", 0, 80700, 26000057)]
    [InlineData(Submitter.ComplianceScheme, RegulatorNation.NorthernIreland, "2025-12-31T23:30:00Z", 2, 86000, 60)]
    [InlineData(Submitter.ComplianceScheme, RegulatorNation.Wales, "2026-01-01T00:00:00Z", 3, 153600, 26000030)]
    public async Task The_seeded_resubmission_rates_give_the_payment_services_fees(
        Submitter submitter, RegulatorNation regulator, string date, int members, decimal total, int rateId)
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();
        var handler = new CalculatePackagingResubmissionFeeHandler(context);

        var result = await handler.HandleAsync(new CalculatePackagingResubmissionFeeQuery(
            new PackagingResubmissionFeeInput(submitter, regulator, DateTimeOffset.Parse(date), members, 1000)), TestContext.Current.CancellationToken);

        Assert.Equal(total, result.Value.TotalResubmissionFee);
        Assert.Equal(total - 1000, result.Value.OutstandingPayment);
        Assert.Equal(rateId, Assert.Single(result.Value.Lines).FeeRateId);
    }

    [Fact]
    public async Task A_resubmission_date_before_the_first_rate_is_a_validation_error()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var result = await new CalculatePackagingResubmissionFeeHandler(context).HandleAsync(new CalculatePackagingResubmissionFeeQuery(
            new PackagingResubmissionFeeInput(Submitter.Producer, RegulatorNation.England, new DateTimeOffset(2023, 12, 31, 0, 0, 0, TimeSpan.Zero), 0, 0)),
            TestContext.Current.CancellationToken);

        Assert.Equal("fees.no_rate_for_date", result.Error?.Code);
    }

    [Fact]
    public async Task No_two_rates_for_the_same_fee_and_regulator_overlap()
    {
        // Stands in for an EXCLUDE constraint, which would need the btree_gist extension.
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        var overlaps = await context.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM fee_rate a
            JOIN fee_rate b
              ON a.id < b.id
             AND a.fee_group_id = b.fee_group_id
             AND a.fee_sub_group_id = b.fee_sub_group_id
             AND a.regulator_id = b.regulator_id
             AND a.effective_from <= b.effective_to
             AND b.effective_from <= a.effective_to
            """).SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, overlaps);
    }

    [Fact]
    public async Task Fee_rates_cannot_be_changed_by_the_app()
    {
        Assert.SkipWhen(database.SkipReason is not null, database.SkipReason ?? "");
        await using var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private FeesDbContext CreateContext() => new(database.Options<FeesDbContext>());
}
