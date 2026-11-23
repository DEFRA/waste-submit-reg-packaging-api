namespace WasteSubmitRegPackagingApi.Utils.Http;

/// <summary>
/// The hosts in a <c>NO_PROXY</c> value, matched the way curl and Node match them. .NET's <c>WebProxy</c> ignores
/// <c>NO_PROXY</c>, so without this every call goes through the proxy, including calls to other CDP services, which
/// the CDP Squid proxy refuses in lower environments.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Entries are separated by commas (spaces around them are ignored) and compared without case.</item>
/// <item><c>*</c> on its own matches every host.</item>
/// <item><c>example.com</c>, <c>.example.com</c> and <c>*.example.com</c> all match <c>example.com</c> and any
/// subdomain of it, such as <c>api.example.com</c>, but not <c>notexample.com</c>.</item>
/// <item>A port on an entry (<c>example.com:8080</c>) is ignored, so the entry matches every port.</item>
/// <item>IP ranges in CIDR form aren't supported; an IP address matches only itself.</item>
/// </list>
/// </remarks>
public sealed class NoProxyRules
{
    private readonly IReadOnlyList<string> _domains;
    private readonly bool _matchesEverything;

    private NoProxyRules(IReadOnlyList<string> domains, bool matchesEverything)
    {
        _domains = domains;
        _matchesEverything = matchesEverything;
    }

    public static NoProxyRules None { get; } = new([], false);

    /// <summary>The entries as they'll be matched, for logging.</summary>
    public IReadOnlyList<string> Domains => _matchesEverything ? ["*"] : _domains;

    public static NoProxyRules Parse(string? noProxy)
    {
        if (string.IsNullOrWhiteSpace(noProxy))
        {
            return None;
        }

        var entries = noProxy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Contains("*"))
        {
            return new NoProxyRules([], true);
        }

        var domains = entries
            .Select(Normalise)
            .Where(domain => domain.Length > 0)
            .Distinct()
            .ToList();

        return new NoProxyRules(domains, false);
    }

    public bool Matches(string host)
    {
        if (_matchesEverything)
        {
            return true;
        }

        // Uri.Host puts brackets round an IPv6 address.
        var candidate = host.Trim().Trim('[', ']').TrimEnd('.').ToLowerInvariant();
        return _domains.Any(domain => candidate == domain || candidate.EndsWith("." + domain, StringComparison.Ordinal));
    }

    private static string Normalise(string entry)
    {
        var domain = entry.ToLowerInvariant();

        if (domain.StartsWith("*.", StringComparison.Ordinal))
        {
            domain = domain[2..];
        }

        domain = domain.TrimStart('.');

        // Drop a port, but leave a bare IPv6 address alone.
        var colon = domain.LastIndexOf(':');
        if (colon > 0 && domain.IndexOf(':') == colon && domain[(colon + 1)..].All(char.IsAsciiDigit))
        {
            domain = domain[..colon];
        }

        return domain.TrimEnd('.');
    }
}
