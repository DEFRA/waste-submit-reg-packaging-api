using System.Reflection;

namespace WasteSubmitRegPackagingApi.Utils.OpenApi;

/// <summary>
/// Whether the app is being started by the build to write the OpenAPI documents (see the <c>OpenApi*</c> settings
/// in the csproj). That run starts the app through the <c>GetDocument.Insider</c> tool, with no environment
/// settings, so start-up checks that need them are skipped. The same check as Manage Obligations.
/// </summary>
public static class OpenApiBuild
{
    public static bool IsRunning { get; } = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
