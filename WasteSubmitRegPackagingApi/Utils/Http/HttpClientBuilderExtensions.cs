using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace WasteSubmitRegPackagingApi.Utils.Http;

/// <summary>
/// The resilience pipeline Manage Obligations (waste-obligations) puts on its downstream clients: an overall
/// timeout, retries for transient failures, and a timeout on each attempt. The values come from the
/// <see cref="HttpStandardResilienceOptions"/> named after the client's config section.
/// </summary>
public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddResiliencePipeline(this IHttpClientBuilder httpClientBuilder, string name)
    {
        httpClientBuilder.AddResilienceHandler(
            name,
            (builder, context) =>
            {
                var options = context.ServiceProvider
                    .GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>()
                    .Get(name);

                builder
                    .AddTimeout(options.TotalRequestTimeout)
                    .AddRetry(options.Retry)
                    .AddTimeout(options.AttemptTimeout);
            });

        // The pipeline owns the timeouts, so HttpClient's own 100 second default must not cut in first.
        return httpClientBuilder.ConfigureHttpClient(httpClient => httpClient.Timeout = Timeout.InfiniteTimeSpan);
    }
}
