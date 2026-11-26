using System.Runtime.CompilerServices;

namespace WasteSubmitRegPackagingApi.Test.Support;

internal static class TestEnvironment
{
    /// <summary>
    /// Every test host reads metrics in-process, so nothing is exported to CloudWatch. A test can still turn
    /// export on with its own <c>AWS_EMF_ENABLED</c> setting.
    /// </summary>
    [ModuleInitializer]
    internal static void DisableMetricsExport() => Environment.SetEnvironmentVariable("AWS_EMF_ENABLED", "false");

    /// <summary>
    /// GitHub Actions sets <c>CI=true</c> and has Docker, so a test that can't reach Docker there must fail: a skip
    /// would hide that the database tests never ran. The Docker image build has no Docker and no <c>CI</c>, so the
    /// same tests skip there.
    /// </summary>
    internal static bool DockerIsRequired =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
}
