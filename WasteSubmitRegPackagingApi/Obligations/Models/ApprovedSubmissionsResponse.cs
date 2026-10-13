using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class ApprovedSubmissionsResponse
{
    [Description("Every approved submission for the year.")]
    public required IReadOnlyList<ApprovedSubmission> ApprovedSubmissions { get; init; }
}