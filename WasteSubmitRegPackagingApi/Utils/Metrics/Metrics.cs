namespace WasteSubmitRegPackagingApi.Utils.Metrics;

/// <summary>Names shared by the metrics and the custom Grafana dashboard. They match Manage Obligations.</summary>
public static class Metrics
{
    public const string MeterName = "Defra.WasteSubmitRegPackagingApi";

    public static class Tags
    {
        public const string Service = nameof(Service);
        public const string HttpMethod = nameof(HttpMethod);
        public const string RequestPath = nameof(RequestPath);
        public const string StatusCode = nameof(StatusCode);
        public const string ExceptionType = nameof(ExceptionType);
    }
}
