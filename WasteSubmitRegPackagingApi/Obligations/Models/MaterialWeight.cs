using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class MaterialWeight
{
    public required string MaterialCode { get; init; }

    /// <summary>
    /// Tonnes, totalled across the accepted H1 and H2 submissions. The packaging data file holds kilograms.
    /// Whether this should be whole or decimal, and gross or net of transitional packaging, is still to be confirmed.
    /// </summary>
    public required decimal Tonnes { get; init; }
}