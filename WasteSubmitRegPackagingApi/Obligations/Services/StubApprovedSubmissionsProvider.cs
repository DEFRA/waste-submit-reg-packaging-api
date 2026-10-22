using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

/// <summary>
/// Serves example responses until the real data source is wired in. The filtered endpoint
/// matches on the submitter's organisation ID (a direct registrant or a compliance scheme).
/// </summary>
public class StubApprovedSubmissionsProvider : IApprovedSubmissionsProvider
{
    public Task<ItemsResponse<ProducerPackaging>?> GetProducerMaterialTotalsAsync(
        int packagingYear,
        Guid submitterOrganisationId,
        CancellationToken cancellationToken)
    {
        if (!ApprovedSubmissionsFixtures.KnownSubmitterIds.Contains(submitterOrganisationId))
        {
            return Task.FromResult<ItemsResponse<ProducerPackaging>?>(null);
        }

        var submission = ApprovedSubmissionsFixtures.ForYear(packagingYear)
            .FirstOrDefault(approvedSubmission => approvedSubmission.SubmitterOrganisationId == submitterOrganisationId);

        ItemsResponse<ProducerPackaging>? response =
            new ItemsResponse<ProducerPackaging> { Items = submission?.Producers ?? [] };

        return Task.FromResult(response);
    }

    public Task<ItemsResponse<ApprovedSubmission>> GetProducerMaterialTotalsBySubmitterAsync(
        int packagingYear,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ItemsResponse<ApprovedSubmission> { Items = ApprovedSubmissionsFixtures.ForYear(packagingYear) });
    }
}