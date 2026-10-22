using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class ProducerPackaging
{
    [Description("The producer the packaging belongs to.")]
    public required Guid ProducerOrganisationId { get; init; }

    [Description("Each time the producer joined or left during the year, oldest first. Empty when it did neither.")]
    public required IReadOnlyList<LeaverJoinerEvent> Events { get; init; }

    [Description("Packaging totals, one per material.")]
    public required IReadOnlyList<MaterialWeight> Materials { get; init; }
}