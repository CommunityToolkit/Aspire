using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class CertificateTrustLoggingTests
{
    [Fact]
    public async Task WithPerlCertificateTrust_InRunMode_LogsCertificateStatus()
    {
        var builder = DistributedApplication.CreateBuilder();
#pragma warning disable CTASPIREPERL001
        builder.AddPerlScript("test-cert", "scripts", "app.pl")
            .WithPerlCertificateTrust();
#pragma warning restore CTASPIREPERL001
        using var app = builder.Build();

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(builder, app, "test-cert");

        Assert.Contains(logs, l =>
            l.Contains("Certificate trust configured") || l.Contains("no SSL_CERT_FILE found"));
    }

    [Fact]
    public async Task WithPerlCertificateTrust_InPublishMode_DoesNotLogCertificateStatus()
    {
        var builder = DistributedApplication.CreateBuilder(
            ["--publisher", "manifest", "--output-path", Path.Combine(Path.GetTempPath(), "aspire-manifest")]);
#pragma warning disable CTASPIREPERL001
        builder.AddPerlScript("test-cert", "scripts", "app.pl")
            .WithPerlCertificateTrust();
#pragma warning restore CTASPIREPERL001
        using var app = builder.Build();

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(
            builder, app, "test-cert", TimeSpan.FromMilliseconds(500));

        Assert.Empty(logs);
    }
}
