using WasteSubmitRegPackagingApi.Utils.Http;

namespace WasteSubmitRegPackagingApi.Test.Utils.Http;

public class ProxyHttpMessageHandlerTest
{
    private const string Squid = "http://localhost:3128";

    private static readonly NoProxyRules CdpNoProxy = NoProxyRules.Parse(".cdp-int.defra.cloud,sqs.eu-west-2.amazonaws.com");

    [Fact]
    public void Calls_to_another_cdp_service_go_direct()
    {
        var proxy = ProxyHttpMessageHandler.CreateProxy(Squid, CdpNoProxy);
        var stub = new Uri("https://waste-submit-stubs.perf-test.cdp-int.defra.cloud/epr-common-data-api/api/submissions/v1/pom/approved-submissions/2025");

        Assert.True(proxy.IsBypassed(stub));
    }

    [Fact]
    public void Calls_to_azure_still_go_through_the_proxy()
    {
        var proxy = ProxyHttpMessageHandler.CreateProxy(Squid, CdpNoProxy);
        var azure = new Uri("https://tstrwdwebwa1415.azurewebsites.net/api/submissions/v1/pom/approved-submissions/2025");

        Assert.False(proxy.IsBypassed(azure));
        Assert.Equal(new Uri(Squid), proxy.GetProxy(azure));
    }

    [Fact]
    public void Local_hosts_still_go_direct_without_no_proxy()
    {
        var proxy = ProxyHttpMessageHandler.CreateProxy(Squid, NoProxyRules.None);

        Assert.True(proxy.IsBypassed(new Uri("http://localhost:8085/health")));
        Assert.False(proxy.IsBypassed(new Uri("https://waste-submit-stubs.perf-test.cdp-int.defra.cloud/")));
    }
}
