using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

/// <summary>
/// The response wrapper shared by every collection endpoint. Paging will sit alongside
/// <see cref="Items"/> once it is needed.
/// </summary>
[ExcludeFromCodeCoverage]
public class ItemsResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }
}