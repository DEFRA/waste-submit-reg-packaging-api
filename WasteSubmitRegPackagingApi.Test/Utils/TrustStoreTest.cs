using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WasteSubmitRegPackagingApi.Utils;

namespace WasteSubmitRegPackagingApi.Test.Utils;

public class TrustStoreTest
{
    [Fact]
    public void Reads_every_certificate_in_a_pem_bundle()
    {
        using var first = NewCertificate("first-ca");
        using var second = NewCertificate("second-ca");
        var bundle = Encoding.ASCII.GetBytes(first.ExportCertificatePem() + "\n" + second.ExportCertificatePem() + "\n");

        var certificates = TrustStore.ReadCertificates(bundle);

        Assert.Equal(["CN=first-ca", "CN=second-ca"], certificates.Select(certificate => certificate.Subject));
    }

    [Fact]
    public void Reads_a_single_der_certificate()
    {
        using var certificate = NewCertificate("only-ca");

        var certificates = TrustStore.ReadCertificates(certificate.Export(X509ContentType.Cert));

        Assert.Equal("CN=only-ca", Assert.Single(certificates).Subject);
    }

    private static X509Certificate2 NewCertificate(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
    }
}
