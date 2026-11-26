using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Fees.Domain;

/// <summary>The <c>fee_group</c> and <c>fee_sub_group</c> ids the code needs. They are epr-payment-service's ids.</summary>
internal static class FeeIds
{
    public const int ProducerResubmissionGroup = 5;
    public const int ComplianceSchemeResubmissionGroup = 6;
    public const int ReSubmittingSubGroup = 7;
}

/// <summary>A kind of fee (<c>fee_group</c>), for example "Producer re-submitting a report".</summary>
internal sealed class FeeGroup
{
    private FeeGroup()
    {
        // For EF Core.
    }

    public FeeGroup(int id, string code, string description)
    {
        Id = id;
        Code = code;
        Description = description;
    }

    public int Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string Description { get; private set; } = null!;
}

/// <summary>
/// A rate (<c>fee_rate</c>) for one fee group, sub-group and regulator over an inclusive date period.
/// Rates are read-only here: Liquibase changesets add and change them.
/// </summary>
internal sealed class FeeRate
{
    private FeeRate()
    {
        // For EF Core.
    }

    public FeeRate(int id, int feeGroupId, int feeSubGroupId, short regulatorId, decimal amount, DateOnly effectiveFrom, DateOnly effectiveTo)
    {
        Id = id;
        FeeGroupId = feeGroupId;
        FeeSubGroupId = feeSubGroupId;
        RegulatorId = regulatorId;
        Amount = amount;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public int Id { get; private set; }

    public int FeeGroupId { get; private set; }

    public int FeeSubGroupId { get; private set; }

    public short RegulatorId { get; private set; }

    /// <summary>In pence.</summary>
    public decimal Amount { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly EffectiveTo { get; private set; }

    public bool Covers(DateOnly date) => date >= EffectiveFrom && date <= EffectiveTo;

    /// <summary>
    /// Picks the rate for a date from one fee's rates, with the payment service's rules: both ends of the
    /// period are inclusive, and if periods overlap the latest <see cref="EffectiveFrom"/> wins.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No rates exist at all for the fee. That's a set-up fault, not a caller error, so it is a 500, as today.
    /// </exception>
    public static Result<FeeRate> SelectFor(IReadOnlyCollection<FeeRate> rates, DateOnly date)
    {
        if (rates.Count == 0)
            throw new InvalidOperationException("No fee rates are set up for this fee type and regulator.");

        var rate = rates
            .Where(r => r.Covers(date))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();

        return rate is null ? FeesErrors.NoRateForDate(date) : rate;
    }
}

internal static class FeesErrors
{
    public static Error NoRateForDate(DateOnly date) =>
        Error.Validation("fees.no_rate_for_date", $"No fee rate covers {date:yyyy-MM-dd}.");

    public static readonly Error MemberCountInvalid =
        Error.Validation("fees.member_count_invalid", "A compliance scheme resubmission needs at least one member.");
}
