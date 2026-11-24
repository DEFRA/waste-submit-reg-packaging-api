using System.Runtime.CompilerServices;

namespace WasteSubmitRegPackagingApi.Test;

internal static class TestEnvironment
{
    /// <summary>
    /// Every test host reads metrics in-process, so nothing is exported to CloudWatch. A test can still turn
    /// export on with its own <c>AWS_EMF_ENABLED</c> setting.
    /// </summary>
    [ModuleInitializer]
    internal static void DisableMetricsExport() => Environment.SetEnvironmentVariable("AWS_EMF_ENABLED", "false");
}
