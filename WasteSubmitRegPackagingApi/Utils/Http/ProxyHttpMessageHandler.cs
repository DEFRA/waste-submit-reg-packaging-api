using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace WasteSubmitRegPackagingApi.Utils.Http;

/// <summary>
/// The primary handler for clients that call outside CDP through the CDP proxy (<c>HTTP_PROXY</c>). Hosts in
/// <c>NO_PROXY</c> are called directly, as curl and Node do: .NET's <c>WebProxy</c> ignores <c>NO_PROXY</c> on its own,
/// and the CDP Squid proxy refuses calls to <c>*.cdp-int.defra.cloud</c> in lower environments, so a call to another
/// CDP service (waste-submit-stubs in perf-test, for example) would otherwise fail.
/// </summary>
[ExcludeFromCodeCoverage]
public class ProxyHttpMessageHandler : HttpClientHandler
{
    [ExcludeFromCodeCoverage]
    public ProxyHttpMessageHandler(ILogger<ProxyHttpMessageHandler> logger)
    {
        var proxyUri = Environment.GetEnvironmentVariable("HTTP_PROXY");
        var noProxy = NoProxyRules.Parse(Environment.GetEnvironmentVariable("NO_PROXY"));

        if (proxyUri != null)
        {
            logger.LogDebug("Creating proxy http client, bypassing the proxy for {NoProxy}", string.Join(",", noProxy.Domains));
        }
        else
        {
            logger.LogWarning("HTTP_PROXY is NOT set, proxy client will be disabled");
        }

        Proxy = CreateProxy(proxyUri, noProxy);
        UseProxy = proxyUri != null;
    }

    /// <summary>The proxy for <paramref name="proxyUri"/>, bypassed for local hosts and for hosts in <paramref name="noProxy"/>.</summary>
    public static IWebProxy CreateProxy(string? proxyUri, NoProxyRules noProxy)
    {
        var proxy = new WebProxy { BypassProxyOnLocal = true };
        if (proxyUri != null)
        {
            proxy.Address = new UriBuilder(proxyUri).Uri;
        }

        return new NoProxyAwareProxy(proxy, noProxy);
    }

    private sealed class NoProxyAwareProxy(IWebProxy inner, NoProxyRules noProxy) : IWebProxy
    {
        public ICredentials? Credentials
        {
            get => inner.Credentials;
            set => inner.Credentials = value;
        }

        public Uri? GetProxy(Uri destination) => IsBypassed(destination) ? null : inner.GetProxy(destination);

        public bool IsBypassed(Uri host) => noProxy.Matches(host.Host) || inner.IsBypassed(host);
    }
}
