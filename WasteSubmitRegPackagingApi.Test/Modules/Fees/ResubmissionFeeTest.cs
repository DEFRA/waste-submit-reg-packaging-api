using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.Fees.Domain;

namespace WasteSubmitRegPackagingApi.Test.Modules.Fees;

/// <summary>
/// The resubmission fee rules, with no database. The cases are ported from epr-payment-service's
/// ProducerResubmissionServiceTests, ComplianceSchemeResubmissionServiceTests and BaseFeeRepositoryTests, so
/// the same inputs give the same results.
/// </summary>
public class ResubmissionFeeTest
{
    private static readonly DateOnly s_2025 = new(2025, 6, 1);

    private static FeeRate Rate(decimal amount, string from = "2024-01-01", string to = "2025-12-31", int id = 1, int group = 6) =>
        new(id, group, FeeIds.ReSubmittingSubGroup, 1, amount, DateOnly.Parse(from), DateOnly.Parse(to));

    // ---- Choosing the rate (BaseFeeRepository.GetFeeAsync) ----

    [Theory]
    [InlineData("2024-01-01")] // first day included
    [InlineData("2025-12-31")] // last day included
    public void A_rate_covers_both_ends_of_its_period(string date)
    {
        var result = FeeRate.SelectFor([Rate(43000)], DateOnly.Parse(date));

        Assert.Equal(43000, result.Value.Amount);
    }

    [Fact]
    public void The_rate_for_the_date_is_chosen_from_contiguous_bands()
    {
        FeeRate[] rates = [Rate(43000, "2024-01-01", "2025-12-31", 57), Rate(51200, "2026-01-01", "2050-12-31", 26000028)];

        Assert.Equal(57, FeeRate.SelectFor(rates, new DateOnly(2025, 12, 31)).Value.Id);
        Assert.Equal(26000028, FeeRate.SelectFor(rates, new DateOnly(2026, 1, 1)).Value.Id);
    }

    [Fact]
    public void When_periods_overlap_the_latest_effective_from_wins()
    {
        FeeRate[] rates = [Rate(100, "2024-01-01", "2026-12-31", 1), Rate(200, "2025-01-01", "2025-12-31", 2)];

        Assert.Equal(200, FeeRate.SelectFor(rates, s_2025).Value.Amount);
    }

    [Fact]
    public void A_date_no_rate_covers_is_a_validation_error()
    {
        var result = FeeRate.SelectFor([Rate(43000)], new DateOnly(2023, 12, 31));

        Assert.Equal("fees.no_rate_for_date", result.Error?.Code);
    }

    [Fact]
    public void No_rates_at_all_is_a_set_up_fault_not_a_caller_error()
    {
        // Today a KeyNotFoundException and a 500. Still an exception, so still a 500.
        Assert.Throws<InvalidOperationException>(() => FeeRate.SelectFor([], s_2025));
    }

    // ---- Compliance scheme (ComplianceSchemeResubmissionService) ----

    [Theory]
    [InlineData(10000, 1, 0, 10000, 10000)]      // WithNoPreviousPayment
    [InlineData(10000, 1, 5000, 10000, 5000)]    // WithPartialPreviousPayment
    [InlineData(10000, 1, 10000, 10000, 0)]      // WithFullPreviousPayment
    [InlineData(10000, 1, 15000, 10000, -5000)]  // WithOverPreviousPayment: not clamped
    [InlineData(10000, 5, 1234, 50000, 48766)]   // ValidRequest: base x 5
    [InlineData(10000, 3, 0, 30000, 30000)]      // CorrectlyCalculateFeeComponents: base x 3
    public void A_compliance_scheme_pays_the_rate_per_member_less_previous_payments(
        decimal rate, int members, decimal previous, decimal total, decimal outstanding)
    {
        var fee = ResubmissionFeeCalculator.Calculate(Submitter.ComplianceScheme, Rate(rate), "CS", members, previous).Value;

        Assert.Equal(total, fee.TotalResubmissionFee);
        Assert.Equal(previous, fee.PreviousPayments);
        Assert.Equal(outstanding, fee.OutstandingPayment);
        Assert.Equal(members, fee.MemberCount);
    }

    [Fact]
    public void A_compliance_scheme_with_no_members_is_a_validation_error()
    {
        var result = ResubmissionFeeCalculator.Calculate(Submitter.ComplianceScheme, Rate(10000), "CS", 0, 0);

        Assert.Equal("fees.member_count_invalid", result.Error?.Code);
    }

    [Fact]
    public void A_compliance_scheme_fee_is_one_line_with_the_rate_and_member_count()
    {
        var fee = ResubmissionFeeCalculator.Calculate(
            Submitter.ComplianceScheme, Rate(51200, id: 26000028), "Compliance Scheme re-submitting a report", 3, 0).Value;

        var line = Assert.Single(fee.Lines);
        Assert.Equal(new FeeLine(26000028, 6, "Compliance Scheme re-submitting a report", 51200, 3, 153600), line);
    }

    // ---- Producer (ProducerResubmissionService, with the member count flag off) ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void A_producer_pays_the_flat_rate_whatever_the_member_count(int members)
    {
        var fee = ResubmissionFeeCalculator.Calculate(Submitter.Producer, Rate(71400, group: 5), "P", members, 0).Value;

        Assert.Equal(71400, fee.TotalResubmissionFee);
        Assert.Equal(1, Assert.Single(fee.Lines).Quantity);
    }

    [Theory]
    [InlineData(0, 71400)]        // WithNoPreviousPayments
    [InlineData(71400, 0)]        // WithFullPreviousPayment
    [InlineData(71900, -500)]     // WithOverpayment
    [InlineData(76400, -5000)]    // WithExcessOverpayment
    public void A_producer_owes_the_rate_less_previous_payments(decimal previous, decimal outstanding)
    {
        var fee = ResubmissionFeeCalculator.Calculate(Submitter.Producer, Rate(71400, group: 5), "P", 0, previous).Value;

        Assert.Equal(outstanding, fee.OutstandingPayment);
    }
}
