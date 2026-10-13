using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class ApprovedSubmissionsResponse
{
    public required IReadOnlyList<ApprovedSubmission> ApprovedSubmissions { get; init; }
}