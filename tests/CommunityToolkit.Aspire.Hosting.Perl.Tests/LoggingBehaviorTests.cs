using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using CommunityToolkit.Aspire.Hosting.Perl;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class LoggingBehaviorTests
{
    /// <summary>
    /// Fires <see cref="BeforeStartEvent"/> on the builder and returns log lines
    /// collected from the resource via <see cref="ResourceLoggerService"/>.
    /// Uses <see cref="EventDispatchBehavior.BlockingConcurrent"/> so that all
    /// handlers execute even when Aspire infrastructure handlers (DCP, etc.)
    /// throw in unit test environments without full tooling configured.
    /// </summary>
    private static async Task<List<string>> PublishBeforeStartAndCollectLogsAsync(
        IDistributedApplicationBuilder builder,
        DistributedApplication app,
        string resourceName,
        TimeSpan? timeout = null)
    {
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var eventing = (DistributedApplicationEventing)builder.Eventing;

        try
        {
            await eventing.PublishAsync(
                new BeforeStartEvent(app.Services, appModel),
                EventDispatchBehavior.BlockingConcurrent,
                CancellationToken.None);
        }
        catch (Exception)
        {
            // Expected: Aspire DCP infrastructure handlers throw in unit test
            // environments that lack the Aspire CLI / dashboard binaries.
        }

        var loggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var logs = new List<string>();
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(3));
        try
        {
            await foreach (var batch in loggerService.WatchAsync(resourceName).WithCancellation(cts.Token))
            {
                logs.AddRange(batch.Select(l => l.Content));
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when no logs are produced (e.g. publish mode).
        }

        return logs;
    }

    #region AddPerlAppCore Logging

    [Fact]
    public async Task AddPerlScript_InRunMode_LogsResourceConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlScript("test-script", "scripts", "app.pl");
        using var app = builder.Build();

        var logs = await PublishBeforeStartAndCollectLogsAsync(builder, app, "test-script");

        Assert.Contains(logs, l => l.Contains("Perl resource 'test-script' configured"));
        Assert.Contains(logs, l => l.Contains("entrypoint=app.pl"));
    }

    [Fact]
    public async Task AddPerlApi_InRunMode_LogsResourceConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlApi("test-api", "api", "server.pl");
        using var app = builder.Build();

        var logs = await PublishBeforeStartAndCollectLogsAsync(builder, app, "test-api");

        Assert.Contains(logs, l => l.Contains("Perl resource 'test-api' configured"));
        Assert.Contains(logs, l => l.Contains("entrypoint=server.pl"));
    }

    [Fact]
    public async Task AddPerlScript_InPublishMode_DoesNotLogResourceConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder(
            ["--publisher", "manifest", "--output-path", Path.Combine(Path.GetTempPath(), "aspire-manifest")]);
        builder.AddPerlScript("test-script", "scripts", "app.pl");
        using var app = builder.Build();

        var logs = await PublishBeforeStartAndCollectLogsAsync(
            builder, app, "test-script", TimeSpan.FromMilliseconds(500));

        Assert.DoesNotContain(logs, l => l.Contains("Perl resource"));
    }

    #endregion

    #region WithPerlCertificateTrust Logging

#pragma warning disable CTASPIREPERL001
    [Fact]
    public async Task WithPerlCertificateTrust_InRunMode_LogsCertificateStatus()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlScript("test-cert", "scripts", "app.pl")
            .WithPerlCertificateTrust();
        using var app = builder.Build();

        var logs = await PublishBeforeStartAndCollectLogsAsync(builder, app, "test-cert");

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

        var logs = await PublishBeforeStartAndCollectLogsAsync(
            builder, app, "test-cert", TimeSpan.FromMilliseconds(500));

        Assert.DoesNotContain(logs, l => l.Contains("Certificate trust"));
        Assert.DoesNotContain(logs, l => l.Contains("SSL_CERT_FILE"));
    }

#pragma warning restore CTASPIREPERL001

    #endregion

    #region WithLocalLib Logging

    [Fact]
    public async Task WithLocalLib_InRunMode_LogsPathResolution()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlScript("test-locallib", "scripts", "app.pl")
            .WithLocalLib("local");
        using var app = builder.Build();

        var logs = await PublishBeforeStartAndCollectLogsAsync(builder, app, "test-locallib");

        // The log records both the configured relative path ('local') and the
        // fully-resolved absolute path, which is rooted under the 'scripts'
        // working directory, e.g. ".../scripts/local".
        Assert.Contains(logs, l =>
            l.Contains("local::lib configured: path='local'") &&
            l.Contains(Path.Combine("scripts", "local")));
    }

    [Fact]
    public async Task WithLocalLib_InPublishMode_DoesNotLogPathResolution()
    {
        var builder = DistributedApplication.CreateBuilder(
            ["--publisher", "manifest", "--output-path", Path.Combine(Path.GetTempPath(), "aspire-manifest")]);
        builder.AddPerlScript("test-locallib", "scripts", "app.pl")
            .WithLocalLib("local");
        using var app = builder.Build();

        var logs = await PublishBeforeStartAndCollectLogsAsync(
            builder, app, "test-locallib", TimeSpan.FromMilliseconds(500));

        Assert.DoesNotContain(logs, l => l.Contains("local::lib configured"));
    }

    #endregion
}
