using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;

namespace CommunityToolkit.Aspire.RavenDB.Client.Tests;

/// <summary>A certificate delivered as a value, as the RavenDB Cloud hosting integration does, instead of a file.</summary>
public class RavenDBClientCertificateTests
{
    private static (string Base64, string Thumbprint) CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=aspire.api", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        return (Convert.ToBase64String(certificate.Export(X509ContentType.Pfx)), certificate.Thumbprint);
    }

    private static IDocumentStore Resolve(params (string Key, string Value)[] settings)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
        [
            new("ConnectionStrings:db", "URL=https://a.example.ravendb.cloud;Database=orders"),
            new("Aspire:RavenDB:Client:DisableHealthChecks", "true"),
            .. settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)),
        ]);

        builder.AddRavenDBClient("db");

        var host = builder.Build();
        return host.Services.GetRequiredService<IDocumentStore>();
    }

    [Fact]
    public void CertificateComesFromTheConnectionsSection()
    {
        var (base64, thumbprint) = CreateCertificate();

        using var store = Resolve(("Aspire:RavenDB:Client:db:CertificateBase64", base64));

        Assert.Equal(thumbprint, store.Certificate.Thumbprint);
    }

    [Fact]
    public void CertificateComesFromTheDefaultSection()
    {
        var (base64, thumbprint) = CreateCertificate();

        using var store = Resolve(("Aspire:RavenDB:Client:CertificateBase64", base64));

        Assert.Equal(thumbprint, store.Certificate.Thumbprint);
    }

    [Fact]
    public void InvalidBase64IsReportedWithoutTheValue()
    {
        var exception = Assert.ThrowsAny<Exception>(() => Resolve(("Aspire:RavenDB:Client:db:CertificateBase64", "not base64!")));

        var invalid = Assert.IsType<InvalidOperationException>(Unwrap(exception));
        Assert.Contains("'CertificateBase64' is not valid base64", invalid.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not base64!", invalid.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentThatIsNotAPfxIsReported()
    {
        var exception = Assert.ThrowsAny<Exception>(() => Resolve(("Aspire:RavenDB:Client:db:CertificateBase64", Convert.ToBase64String("not a pfx"u8.ToArray()))));

        Assert.Contains("cannot be loaded", Assert.IsType<InvalidOperationException>(Unwrap(exception)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpsWithoutACertificateNamesEveryWayToGiveOne()
    {
        var exception = Assert.ThrowsAny<Exception>(() => Resolve());

        Assert.Contains("'RavenDBClientSettings.CertificateBase64'", Unwrap(exception).Message, StringComparison.Ordinal);
    }

    private static Exception Unwrap(Exception exception) =>
        exception is System.Reflection.TargetInvocationException or AggregateException && exception.InnerException is { } inner ? Unwrap(inner) : exception;
}
