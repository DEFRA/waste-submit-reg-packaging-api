using System.Diagnostics;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;

namespace WasteSubmitRegPackagingApi.Utils.Metrics;

/// <summary>
/// Counts requests and faults and times each request, under the names Manage Obligations uses, so its
/// dashboard panels work here too.
/// </summary>
public class RequestMetrics : IRequestMetrics
{
    private static readonly string s_service = Process.GetCurrentProcess().ProcessName;

    private readonly Counter<long> _requestsReceived;
    private readonly Counter<long> _requestsFaulted;
    private readonly Histogram<double> _requestDuration;

    public RequestMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Metrics.MeterName);

        _requestsReceived = meter.CreateCounter<long>("RequestReceived", nameof(Unit.COUNT), "Count of requests received");
        _requestsFaulted = meter.CreateCounter<long>("RequestFaulted", nameof(Unit.COUNT), "Count of request faults");
        _requestDuration = meter.CreateHistogram<double>("RequestDuration", nameof(Unit.MILLISECONDS), "Duration of request");
    }

    public void RequestCompleted(string requestPath, string httpMethod, int statusCode, double milliseconds)
    {
        var tags = BuildTags(requestPath, httpMethod, statusCode);

        _requestsReceived.Add(1, tags);
        _requestDuration.Record(milliseconds, tags);
    }

    public void RequestFaulted(string requestPath, string httpMethod, int statusCode, Exception exception)
    {
        var tags = BuildTags(requestPath, httpMethod, statusCode);
        tags.Add(Metrics.Tags.ExceptionType, exception.GetType().Name);

        _requestsFaulted.Add(1, tags);
    }

    private static TagList BuildTags(string requestPath, string httpMethod, int statusCode) =>
        new()
        {
            { Metrics.Tags.Service, s_service },
            { Metrics.Tags.RequestPath, requestPath },
            { Metrics.Tags.HttpMethod, httpMethod },
            { Metrics.Tags.StatusCode, statusCode },
        };
}
