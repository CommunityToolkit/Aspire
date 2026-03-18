using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlAppCoreLoggingTests
{
    [Fact]
    public async Task AddPerlScript_InRunMode_LogsResourceConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlScript("test-script", "scripts", "app.pl");
        using var app = builder.Build();

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(builder, app, "test-script");

        Assert.Contains(logs, l => l.Contains("Perl resource 'test-script' configured"));
        Assert.Contains(logs, l => l.Contains("entrypoint=app.pl"));
    }

    [Fact]
    public async Task AddPerlApi_InRunMode_LogsResourceConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlApi("test-api", "api", "server.pl");
        using var app = builder.Build();

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(builder, app, "test-api");

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

        var logs = await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(
            builder, app, "test-script", TimeSpan.FromMilliseconds(500));

        Assert.DoesNotContain(logs, l => l.Contains("Perl resource"));
    }
}
