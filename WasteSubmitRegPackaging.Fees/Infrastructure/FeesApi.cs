using WasteSubmitRegPackaging.Fees.Application.Queries.CalculatePackagingResubmissionFee;
using WasteSubmitRegPackaging.Fees.Contracts;
using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SharedKernel.Results;

namespace WasteSubmitRegPackaging.Fees.Infrastructure;

/// <summary>
/// The public contract, implemented by the same query handler the endpoint uses, so a module and an HTTP caller
/// always get the same answer.
/// </summary>
internal sealed class FeesApi(
    IQueryHandler<CalculatePackagingResubmissionFeeQuery, ResubmissionFee> calculateResubmissionFee) : IFeesApi
{
    public Task<Result<ResubmissionFee>> CalculatePackagingResubmissionFeeAsync(
        PackagingResubmissionFeeInput input, CancellationToken cancellationToken) =>
        calculateResubmissionFee.HandleAsync(new CalculatePackagingResubmissionFeeQuery(input), cancellationToken);
}
