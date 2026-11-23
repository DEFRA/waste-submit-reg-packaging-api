using WasteSubmitRegPackagingApi.Utils.Http;

namespace WasteSubmitRegPackagingApi.Test.Utils.Http;

public class NoProxyRulesTest
{
    // The value cdp-app-config gives this service in every environment (defaults.env).
    private const string CdpNoProxy =
        ".cdp-int.defra.cloud,.s3.eu-west-2.amazonaws.com,sqs.eu-west-2.amazonaws.com,sns.eu-west-2.amazonaws.com,dynamodb.eu-west-2.amazonaws.com";

    [Theory]
    [InlineData("waste-submit-stubs.perf-test.cdp-int.defra.cloud")]
    [InlineData("epr-azure-stub.perf-test.cdp-int.defra.cloud")]
    [InlineData("WASTE-SUBMIT-STUBS.PERF-TEST.CDP-INT.DEFRA.CLOUD")]
    [InlineData("sqs.eu-west-2.amazonaws.com")]
    [InlineData("my-bucket.s3.eu-west-2.amazonaws.com")]
    public void Cdp_value_bypasses_internal_and_listed_aws_hosts(string host)
    {
        Assert.True(NoProxyRules.Parse(CdpNoProxy).Matches(host));
    }

    [Theory]
    [InlineData("tstrwdwebwa1415.azurewebsites.net")]
    [InlineData("prerwdwebwa1415.azurewebsites.net")]
    [InlineData("cdp-int.defra.cloud.example.com")]
    [InlineData("notcdp-int.defra.cloud")]
    public void Cdp_value_still_proxies_external_hosts(string host)
    {
        Assert.False(NoProxyRules.Parse(CdpNoProxy).Matches(host));
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData(".example.com")]
    [InlineData("*.example.com")]
    [InlineData(" example.com ")]
    [InlineData("example.com:8443")]
    [InlineData("EXAMPLE.COM")]
    public void Every_form_of_an_entry_matches_the_domain_and_its_subdomains(string entry)
    {
        var rules = NoProxyRules.Parse(entry);

        Assert.True(rules.Matches("example.com"));
        Assert.True(rules.Matches("api.example.com"));
        Assert.True(rules.Matches("example.com."));
        Assert.False(rules.Matches("notexample.com"));
        Assert.False(rules.Matches("example.com.evil.net"));
    }

    [Fact]
    public void Star_matches_every_host()
    {
        Assert.True(NoProxyRules.Parse("localhost, *").Matches("anything.example.org"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(",,")]
    public void Empty_value_matches_nothing(string? value)
    {
        Assert.False(NoProxyRules.Parse(value).Matches("waste-submit-stubs.perf-test.cdp-int.defra.cloud"));
    }

    [Fact]
    public void Ip_address_matches_only_itself()
    {
        var rules = NoProxyRules.Parse("10.0.0.1,::1");

        Assert.True(rules.Matches("10.0.0.1"));
        Assert.False(rules.Matches("10.0.0.10"));
        Assert.True(rules.Matches("::1"));
    }
}
