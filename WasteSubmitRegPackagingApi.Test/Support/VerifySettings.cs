using System.Runtime.CompilerServices;

namespace WasteSubmitRegPackagingApi.Test.Support;

/// <summary>Verify snapshot settings, as Manage Obligations.</summary>
public static class VerifySettings
{
    [ModuleInitializer]
    public static void Initialise()
    {
        // Snapshots are real JSON (.verified.json), so the v1 snapshot is itself a valid OpenAPI document.
        VerifierSettings.UseStrictJson();
        VerifierSettings.DontIgnoreEmptyCollections();
    }
}
