using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Logger;
using Amazon.CloudWatch.EMF.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace WasteSubmitRegPackagingApi.Utils.Metrics;

/// <summary>
/// Writes each measurement from this service's meter as an AWS Embedded Metric Format (EMF) log line, which CDP
/// turns into a CloudWatch metric in the service's namespace. Copied from Manage Obligations.
/// </summary>
public static class MetricsExporter
{
    private static readonly MeterListener s_meterListener = new();

    private static ILogger s_logger = NullLogger.Instance;
    private static ILoggerFactory s_loggerFactory = NullLoggerFactory.Instance;
    private static string? s_awsNamespace;

    public static void Init(ILoggerFactory loggerFactory, string awsNamespace)
    {
        s_logger = loggerFactory.CreateLogger(nameof(MetricsExporter));
        s_loggerFactory = loggerFactory;
        s_awsNamespace = awsNamespace;

        s_meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name is Metrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        s_meterListener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);
        s_meterListener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        s_meterListener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
        s_meterListener.Start();
    }

    private static void OnMeasurementRecorded<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        // A metric that can't be written must never fail the request that recorded it.
        try
        {
            using var metricsLogger = new MetricsLogger(s_loggerFactory);
            metricsLogger.SetNamespace(s_awsNamespace);

            var dimensionSet = new DimensionSet();
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag.Value?.ToString()))
                    continue;

                dimensionSet.AddDimension(tag.Key, tag.Value.ToString());
            }

            metricsLogger.SetDimensions(dimensionSet);
            metricsLogger.PutMetric(instrument.Name, Convert.ToDouble(measurement), Enum.Parse<Unit>(instrument.Unit!));
            metricsLogger.Flush();
        }
        catch (Exception ex)
        {
            s_logger.LogError(ex, "Failed to push EMF metric");
        }
    }
}
