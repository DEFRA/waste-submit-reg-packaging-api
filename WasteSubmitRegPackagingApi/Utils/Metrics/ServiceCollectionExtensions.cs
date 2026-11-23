namespace WasteSubmitRegPackagingApi.Utils.Metrics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRequestMetrics(this IServiceCollection services)
    {
        services.AddMetrics();
        services.AddTransient<MetricsMiddleware>();
        services.AddSingleton<IRequestMetrics, RequestMetrics>();

        return services;
    }
}
