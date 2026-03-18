using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class CertificateTrustLoggingTests
{
#pragma warning disable CTASPIREPERL001
    [Fact]
    public async Task WithPerlCertificateTrust_InRunMode_LogsCertificateStatus()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlScript("test-cert", "scripts", "app.pl")
            .WithPerlCertificateTrust();
        using var app = builder.Build();

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(builder, app, "test-cert");

        Assert.True(
            logs.Any(l => l.Contains("Certificate trust configured")) ||
            logs.Any(l => l.Contains("no SSL_CERT_FILE found")),
            "Expected certificate trust logging in run mode");
    }

    [Fact]
    public async Task WithPerlCertificateTrust_InPublishMode_DoesNotLogCertificateStatus()
    {
        var builder = DistributedApplication.CreateBuilder(
            ["--publisher", "manifest", "--output-path", Path.Combine(Path.GetTempPath(), "aspire-manifest")]);
        builder.AddPerlScript("test-cert", "scripts", "app.pl")
            .WithPerlCertificateTrust();
        using var app = builder.Build();

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(
            builder, app, "test-cert", TimeSpan.FromMilliseconds(500));

        Assert.DoesNotContain(logs, l => l.Contains("Certificate trust"));
        Assert.DoesNotContain(logs, l => l.Contains("SSL_CERT_FILE"));
    }

#pragma warning restore CTASPIREPERL001
}
