using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

public interface IApprovedSubmissionsProvider
{
    /// <summary>Null when the submitter organisation does not exist; empty items when it has no approved submission for the year.</summary>
    Task<ItemsResponse<OrganisationPackaging>?> GetAggregatedSubmissionAsync(
        int year,
        Guid organisationId,
        CancellationToken cancellationToken);

    /// <summary>Empty items when the year has no approved submissions.</summary>
    Task<ItemsResponse<ApprovedSubmission>> GetAggregatedSubmissionsAsync(
        int year,
        CancellationToken cancellationToken);
}