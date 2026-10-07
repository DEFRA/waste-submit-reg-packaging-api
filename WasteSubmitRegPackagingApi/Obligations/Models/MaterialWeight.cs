using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class MaterialWeight
{
    public required string MaterialCode { get; init; }

    /// <summary>Kilograms in whole numbers, as in the packaging data file, totalled across the accepted H1 and H2 submissions.</summary>
    public required long Weight { get; init; }
}