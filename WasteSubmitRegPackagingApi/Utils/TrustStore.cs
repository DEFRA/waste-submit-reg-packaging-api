using System.Collections;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Utils;

[ExcludeFromCodeCoverage]
public static class TrustStore
{
    public static void LoadCustomTrustStoreFromEnvironment(this IServiceCollection _)
    {
        var certificates = GetCertificates();
        AddCertificates(certificates);
    }

    private static List<byte[]> GetCertificates()
    {
        return Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .Where(entry =>
                entry.Key.ToString()!.StartsWith("TRUSTSTORE_", StringComparison.Ordinal) && IsBase64String(entry.Value?.ToString() ?? ""))
            .Select(entry => Convert.FromBase64String(entry.Value?.ToString() ?? "")).ToList();
    }

    private static void AddCertificates(List<byte[]> certificates)
    {
        if (certificates.Count == 0) return; // to stop trust store access denied issues on Macs
        var certificateCollection = new X509Certificate2Collection();

        foreach (var certificate in certificates)
        {
            certificateCollection.AddRange(ReadCertificates(certificate));
        }

        var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        try
        {
            store.Open(OpenFlags.ReadWrite);
            store.AddRange(certificateCollection);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Root certificate import failed: " + ex.Message, ex);
        }
        finally
        {
            store.Close();
        }
    }

    /// <summary>
    /// Reads every certificate in a value. A PEM bundle, such as the RDS root CAs, can hold several,
    /// and X509CertificateLoader.LoadCertificate only reads the first.
    /// </summary>
    public static X509Certificate2Collection ReadCertificates(byte[] data)
    {
        var text = Encoding.ASCII.GetString(data);

        if (!text.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal))
        {
            return new X509Certificate2Collection(X509CertificateLoader.LoadCertificate(data));
        }

        var certificates = new X509Certificate2Collection();
        certificates.ImportFromPem(text);
        return certificates;
    }

    private static bool IsBase64String(string str)
    {
        var buffer = new Span<byte>(new byte[str.Length]);
        return Convert.TryFromBase64String(str, buffer, out _);
    }
}