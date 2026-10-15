using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class MaterialWeight
{
    [Description("The two-letter material code, for example PL for plastic or GL for glass.")]
    public required string MaterialCode { get; init; }

    /// <summary>
    /// Kilograms, totalled across the accepted H1 and H2 submissions. The packaging data file only accepts whole
    /// kilograms, so the total is whole too. Whether this is gross or net of transitional packaging is still to be confirmed.
    /// </summary>
    [Description("Kilograms, totalled across the accepted H1 and H2 submissions. Always a whole number.")]
    public required long Kilograms { get; init; }
}