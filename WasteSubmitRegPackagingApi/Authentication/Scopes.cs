using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Authentication;

[ExcludeFromCodeCoverage]
public static class Scopes
{
    public const string Read = "read";

    /// <summary>Commands: the module endpoints that change state.</summary>
    public const string Write = "write";

    public static readonly IReadOnlyList<string> All = [Read, Write];
}
