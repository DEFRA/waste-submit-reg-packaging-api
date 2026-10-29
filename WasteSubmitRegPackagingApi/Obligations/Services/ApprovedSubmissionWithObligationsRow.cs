using System.Diagnostics.CodeAnalysis;
using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

/// <summary>
/// One row from epr-common-data-api's <c>v1/pom/approved-submissions/{periodYear}</c> endpoint: a single
/// producer/material combination, carrying that producer's current obligation state (consistent across all
/// of its material rows, since the underlying stored procedure keeps at most one obligation row per producer).
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ApprovedSubmissionWithObligationsRow
{
    public required SubmitterType SubmitterType { get; init; }

    public required Guid SubmitterId { get; init; }

    public required Guid OrganisationId { get; init; }

    public required string PackagingMaterial { get; init; }

    public required long PackagingMaterialWeightKg { get; init; }

    /// <summary>The raw registration-file leaver_code value, with or without a leading zero. Goes through <see cref="LeaverJoinerCodeMapper"/>.</summary>
    public string? LeaverCode { get; init; }

    public DateTime? JoinerDate { get; init; }

    public DateTime? LeaverDate { get; init; }
}
