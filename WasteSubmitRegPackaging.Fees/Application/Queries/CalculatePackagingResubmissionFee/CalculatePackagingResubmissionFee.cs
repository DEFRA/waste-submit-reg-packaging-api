using Microsoft.EntityFrameworkCore;
using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.Fees.Domain;
using WasteSubmitRegPackaging.Fees.Infrastructure;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Fees.Application.Queries.CalculatePackagingResubmissionFee;

/// <summary>A query, not a command: working out a fee changes nothing. The caller stores the result.</summary>
internal sealed record CalculatePackagingResubmissionFeeQuery(PackagingResubmissionFeeInput Input) : IQuery<ResubmissionFee>;

internal sealed class CalculatePackagingResubmissionFeeHandler(FeesDbContext dbContext)
    : IQueryHandler<CalculatePackagingResubmissionFeeQuery, ResubmissionFee>
{
    public async Task<Result<ResubmissionFee>> HandleAsync(
        CalculatePackagingResubmissionFeeQuery query, CancellationToken cancellationToken)
    {
        var input = query.Input;
        var feeGroupId = ResubmissionFeeCalculator.FeeGroupFor(input.Submitter);
        var regulatorId = (short)input.Regulator;

        // All of this fee's rates for the regulator (a handful of rows), then the date rule in the domain.
        var rates = await dbContext.FeeRates
            .Where(r => r.FeeGroupId == feeGroupId
                        && r.FeeSubGroupId == FeeIds.ReSubmittingSubGroup
                        && r.RegulatorId == regulatorId)
            .ToListAsync(cancellationToken);

        // The UTC calendar date, as the payment service matches it. Whether it should be the UK date is an
        // open question on the ticket.
        var rate = FeeRate.SelectFor(rates, DateOnly.FromDateTime(input.ResubmissionDate.UtcDateTime));
        if (rate.IsFailure)
            return rate.Error;

        var description = await dbContext.FeeGroups
            .Where(g => g.Id == feeGroupId)
            .Select(g => g.Description)
            .SingleAsync(cancellationToken);

        return ResubmissionFeeCalculator.Calculate(
            input.Submitter, rate.Value, description, input.MemberCount, input.PreviousPayments);
    }
}
