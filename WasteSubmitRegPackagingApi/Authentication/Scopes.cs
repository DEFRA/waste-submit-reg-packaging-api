using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Authentication;

[ExcludeFromCodeCoverage]
public static class Scopes
{
    public const string Read = "read";

    public static readonly IReadOnlyList<string> All = [Read];
}
