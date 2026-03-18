using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class LocalLibLoggingTests
{
    [Fact]
    public async Task WithLocalLib_InRunMode_LogsPathResolution()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlScript("test-locallib", "scripts", "app.pl")
            .WithLocalLib("local");
        using var app = builder.Build();

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(builder, app, "test-locallib");

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

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(
            builder, app, "test-locallib", TimeSpan.FromMilliseconds(500));

        Assert.DoesNotContain(logs, l => l.Contains("local::lib configured"));
    }
}
