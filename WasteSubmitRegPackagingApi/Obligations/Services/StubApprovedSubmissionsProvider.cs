using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

/// <summary>
/// Serves example responses until the real data source is wired in.
/// The examples are already aggregated, so <c>aggregate</c> is accepted but ignored.
/// </summary>
public class StubApprovedSubmissionsProvider : IApprovedSubmissionsProvider
{
    public Task<ItemsResponse<OrganisationPackaging>?> GetApprovedSubmissionsAsync(
        Guid organisationId,
        int packagingYear,
        bool aggregate,
        CancellationToken cancellationToken)
    {
        var response = ApprovedSubmissionsFixtures.Find(organisationId, packagingYear);

        return Task.FromResult(response);
    }
}