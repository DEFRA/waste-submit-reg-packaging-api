using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class OrganisationPackaging
{
    [Description("The organisation the packaging belongs to.")]
    public required Guid OrganisationId { get; init; }

    [Description("Why the organisation joined during the year, numbered as in the GOV.UK registration file guidance: 1, 2, 3, 7, 9, 15, 17, 19 or 20. Null if it did not join.")]
    public JoinerCode? JoinerCode { get; init; }

    [Description("When the organisation joined. Null if it did not join.")]
    public DateOnly? JoinerDate { get; init; }

    [Description("Why the organisation left during the year, numbered as in the GOV.UK registration file guidance: 4, 5, 6, 8, 10, 11, 12, 13, 14, 16 or 21. Null if it did not leave.")]
    public LeaverCode? LeaverCode { get; init; }

    [Description("When the organisation left. Null if it did not leave.")]
    public DateOnly? LeaverDate { get; init; }

    [Description("Packaging totals, one per material.")]
    public required IReadOnlyList<MaterialWeight> Materials { get; init; }
}