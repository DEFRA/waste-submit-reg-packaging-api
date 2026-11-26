using WasteSubmitRegPackaging.SharedKernel.Messaging;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetPackagingSubmissionPeriods;
using WasteSubmitRegPackaging.SubmissionPeriods.Application.Queries.GetRegistrationSubmissionPeriods;
using WasteSubmitRegPackaging.SubmissionPeriods.Contracts;

namespace WasteSubmitRegPackaging.SubmissionPeriods.Infrastructure;

/// <summary>
/// The public contract, implemented by the same query handlers the endpoints use, so a module and an HTTP caller
/// always get the same answer.
/// </summary>
internal sealed class SubmissionPeriodsApi(
    IQueryHandler<GetPackagingSubmissionPeriodsQuery, IReadOnlyList<PackagingSubmissionPeriod>> getPackagingSubmissionPeriods,
    IQueryHandler<GetRegistrationSubmissionPeriodsQuery, IReadOnlyList<RegistrationSubmissionPeriod>> getRegistrationSubmissionPeriods)
    : ISubmissionPeriodsApi
{
    public async Task<IReadOnlyList<PackagingSubmissionPeriod>> GetPackagingSubmissionPeriodsAsync(CancellationToken cancellationToken) =>
        (await getPackagingSubmissionPeriods.HandleAsync(new GetPackagingSubmissionPeriodsQuery(), cancellationToken)).Value;

    public async Task<PackagingSubmissionPeriod?> FindPackagingSubmissionPeriodByCodeAsync(
        string periodCode, CancellationToken cancellationToken) =>
        (await getPackagingSubmissionPeriods.HandleAsync(new GetPackagingSubmissionPeriodsQuery(periodCode), cancellationToken)).Value.SingleOrDefault();

    public async Task<IReadOnlyList<RegistrationSubmissionPeriod>> GetRegistrationSubmissionPeriodsAsync(
        int? registrationYear, CancellationToken cancellationToken) =>
        (await getRegistrationSubmissionPeriods.HandleAsync(new GetRegistrationSubmissionPeriodsQuery(registrationYear), cancellationToken)).Value;
}