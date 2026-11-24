using System.Collections.Concurrent;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace WasteSubmitRegPackagingApi.Test.Utils.Logging;

/// <summary>
/// Collects the app's Serilog events so tests can check what was logged. The app reads its sinks from configuration,
/// so a test adds <see cref="Settings"/> to its factory. Each test finds its own events by the trace header it sent.
/// <para>
/// Serilog's request logging writes through the static <c>Log.Logger</c>, which every test host replaces as it
/// starts. A test that reads the request log must therefore be in the <see cref="CapturedLogsCollection"/>, so no
/// other host starts while it runs.
/// </para>
/// </summary>
public static class CapturedLogs
{
    private static readonly ConcurrentQueue<LogEvent> s_events = new();

    public static IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>
    {
        ["Serilog:Using:99"] = typeof(CapturedLogs).Assembly.GetName().Name,
        ["Serilog:WriteTo:99"] = nameof(CapturedLogsSinkExtensions.CapturedLogs)
    };

    internal static void Add(LogEvent logEvent) => s_events.Enqueue(logEvent);

    /// <summary>
    /// The request log line Serilog writes when it finishes a request carrying this trace header value. It is written
    /// as the request completes, so it may land just after the caller has the response.
    /// </summary>
    public static async Task<LogEvent?> RequestCompleted(string traceId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var logEvent = Find(traceId);
            if (logEvent != null)
                return logEvent;
            await Task.Delay(50, cancellationToken);
        }

        return Find(traceId);
    }

    private static LogEvent? Find(string traceId) =>
        s_events.FirstOrDefault(e =>
            e.Properties.TryGetValue("SourceContext", out var source)
            && source is ScalarValue { Value: "Serilog.AspNetCore.RequestLoggingMiddleware" }
            && e.Properties.TryGetValue("CorrelationId", out var id)
            && id is ScalarValue { Value: string value }
            && value == traceId);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CapturedLogsCollection
{
    public const string Name = "Captured logs";
}

public static class CapturedLogsSinkExtensions
{
    // Found by name through the Serilog:Using and Serilog:WriteTo settings above.
    public static LoggerConfiguration CapturedLogs(this LoggerSinkConfiguration sinkConfiguration) =>
        sinkConfiguration.Sink(new Sink());

    private sealed class Sink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => Logging.CapturedLogs.Add(logEvent);
    }
}
