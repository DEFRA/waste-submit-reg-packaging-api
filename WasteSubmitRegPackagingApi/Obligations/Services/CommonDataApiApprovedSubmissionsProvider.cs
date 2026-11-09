using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

/// <summary>
/// Calls epr-common-data-api's <c>v1/pom/approved-submissions/{periodYear}</c> endpoint and groups its flat
/// rows into the nested submitter/producer shape this API serves.
/// </summary>
/// <remarks>
/// The underlying stored procedure keeps only one obligation row per producer (no tie-break between multiple
/// real joiner/leaver events yet - see SUB-410's producer_id fix candidate), so every producer here gets at
/// most one <see cref="LeaverJoinerEvent"/>, not its full join/leave history. This will start returning more
/// than one once the fix candidate is validated and cut over.
///
/// <see cref="GetProducerMaterialTotalsAsync"/> never returns null (never 404s): epr-common-data-api only
/// knows about organisations that submitted, so it cannot tell "this organisation doesn't exist" apart from
/// "this organisation exists but has nothing approved this year". Flagged as an open gap in the SUB-410 plan.
/// </remarks>
public class CommonDataApiApprovedSubmissionsProvider(HttpClient httpClient) : IApprovedSubmissionsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ItemsResponse<ProducerPackaging>?> GetProducerMaterialTotalsAsync(
        int packagingYear,
        Guid submitterOrganisationId,
        CancellationToken cancellationToken)
    {
        var bySubmitter = await GetProducerMaterialTotalsBySubmitterAsync(packagingYear, cancellationToken);

        var submission = bySubmitter.Items.FirstOrDefault(approvedSubmission => approvedSubmission.SubmitterOrganisationId == submitterOrganisationId);

        return new ItemsResponse<ProducerPackaging> { Items = submission?.Producers ?? [] };
    }

    public async Task<ItemsResponse<ApprovedSubmission>> GetProducerMaterialTotalsBySubmitterAsync(
        int packagingYear,
        CancellationToken cancellationToken)
    {
        var rows = await GetRowsAsync(packagingYear, cancellationToken);

        var submissions = rows
            .GroupBy(row => (row.SubmitterId, row.SubmitterType))
            .Select(submitterGroup => new ApprovedSubmission
            {
                SubmitterOrganisationId = submitterGroup.Key.SubmitterId,
                SubmitterType = submitterGroup.Key.SubmitterType,
                Producers = submitterGroup
                    .GroupBy(row => row.OrganisationId)
                    .Select(ToProducerPackaging)
                    .ToList()
            })
            .ToList();

        return new ItemsResponse<ApprovedSubmission> { Items = submissions };
    }

    private async Task<IReadOnlyList<ApprovedSubmissionWithObligationsRow>> GetRowsAsync(int packagingYear, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"v1/pom/approved-submissions/{packagingYear}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<ApprovedSubmissionWithObligationsRow>>(JsonOptions, cancellationToken) ?? [];
    }

    private static ProducerPackaging ToProducerPackaging(IGrouping<Guid, ApprovedSubmissionWithObligationsRow> producerGroup)
    {
        // Obligation fields are the same on every row for a producer, so any row's values do.
        var obligation = producerGroup.First();

        return new ProducerPackaging
        {
            ProducerOrganisationId = producerGroup.Key,
            Events = ToEvents(obligation),
            Materials = producerGroup
                .GroupBy(row => row.PackagingMaterial)
                .Select(materialGroup => new MaterialWeight
                {
                    MaterialCode = materialGroup.Key,
                    Kilograms = materialGroup.Sum(row => row.PackagingMaterialWeightKg)
                })
                .ToList()
        };
    }

    private static IReadOnlyList<LeaverJoinerEvent> ToEvents(ApprovedSubmissionWithObligationsRow obligation)
    {
        var mapped = LeaverJoinerCodeMapper.Map(obligation.LeaverCode);
        if (mapped is null)
        {
            return [];
        }

        var date = mapped.Action == EventAction.Joined ? obligation.JoinerDate : obligation.LeaverDate;
        if (date is null)
        {
            return [];
        }

        return [new LeaverJoinerEvent { Code = mapped.Code, Action = mapped.Action, Date = DateOnly.FromDateTime(date.Value) }];
    }
}
