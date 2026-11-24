namespace WasteSubmitRegPackagingApi.Utils.Metrics;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Records request metrics and, unless <c>AWS_EMF_ENABLED</c> is false, exports them to CloudWatch through EMF.
    /// CDP sets <c>AWS_EMF_NAMESPACE</c> to the service name. Locally, <c>AWS_EMF_ENVIRONMENT=Local</c> writes the
    /// metrics to stdout instead, under the program's namespace if none is set.
    /// </summary>
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<MetricsMiddleware>();

        var config = builder.ApplicationServices.GetRequiredService<IConfiguration>();
        if (!config.GetValue("AWS_EMF_ENABLED", true))
            return builder;

        var awsNamespace = config.GetValue<string>("AWS_EMF_NAMESPACE");
        var environment = config.GetValue<string>("AWS_EMF_ENVIRONMENT") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(awsNamespace) && environment.Equals("Local"))
            awsNamespace = typeof(Program).Assembly.GetName().Name;

        if (string.IsNullOrWhiteSpace(awsNamespace))
            throw new InvalidOperationException("AWS_EMF_NAMESPACE is not set but metrics are enabled");

        MetricsExporter.Init(builder.ApplicationServices.GetRequiredService<ILoggerFactory>(), awsNamespace);

        return builder;
    }
}
