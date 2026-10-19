using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

public interface IApprovedSubmissionsProvider
{
    Task<ItemsResponse<OrganisationPackaging>?> GetAggregatedSubmissionAsync(
        int year,
        Guid organisationId,
        CancellationToken cancellationToken);

    Task<ItemsResponse<ApprovedSubmission>?> GetAggregatedSubmissionsAsync(
        int year,
        CancellationToken cancellationToken);
}