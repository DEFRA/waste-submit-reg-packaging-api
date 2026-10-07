using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

public interface IApprovedSubmissionsProvider
{
    Task<ItemsResponse<OrganisationPackaging>?> GetApprovedSubmissionsAsync(
        Guid organisationId,
        int packagingYear,
        bool aggregate,
        CancellationToken cancellationToken);
}