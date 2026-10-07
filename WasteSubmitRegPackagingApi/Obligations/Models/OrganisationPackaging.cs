using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class OrganisationPackaging
{
    public required Guid OrganisationId { get; init; }

    public JoinerCode? JoinerCode { get; init; }

    public DateOnly? JoinerDate { get; init; }

    public LeaverCode? LeaverCode { get; init; }

    public DateOnly? LeaverDate { get; init; }

    public required IReadOnlyList<MaterialWeight> Materials { get; init; }
}