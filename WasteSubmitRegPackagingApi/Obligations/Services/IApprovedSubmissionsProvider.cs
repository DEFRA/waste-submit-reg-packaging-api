using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

public interface IApprovedSubmissionsProvider
{
    /// <summary>Null when the submitter organisation does not exist; empty items when it has no approved submission for the year.</summary>
    Task<ItemsResponse<ProducerPackaging>?> GetProducerMaterialTotalsAsync(
        int packagingYear,
        Guid submitterOrganisationId,
        CancellationToken cancellationToken);

    /// <summary>Empty items when the year has no approved submissions.</summary>
    Task<ItemsResponse<ApprovedSubmission>> GetProducerMaterialTotalsBySubmitterAsync(
        int packagingYear,
        CancellationToken cancellationToken);
}