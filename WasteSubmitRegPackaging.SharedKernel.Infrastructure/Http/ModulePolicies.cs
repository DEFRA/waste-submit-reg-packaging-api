namespace WasteSubmitRegPackaging.SharedKernel.Infrastructure.Http;

/// <summary>
/// The authorisation policy names modules put on their endpoints. The host registers the policies (see
/// <c>WasteSubmitRegPackagingApi.Authentication</c>), and a test checks the names match.
/// Modules can't reference the host, so the names live here.
/// </summary>
public static class ModulePolicies
{
    /// <summary>Queries.</summary>
    public const string Read = "Read";

    /// <summary>Commands.</summary>
    public const string Write = "Write";
}
