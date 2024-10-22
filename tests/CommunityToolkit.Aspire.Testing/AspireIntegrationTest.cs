using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Testing;

public class AspireIntegrationTestFixture<TEntryPoint>() : DistributedApplicationFactory(typeof(TEntryPoint), []), IAsyncLifetime where TEntryPoint : class
{
    public ResourceNotificationService ResourceNotificationService => App.Services.GetRequiredService<ResourceNotificationService>();

    public DistributedApplication App { get; private set; } = null!;

    protected override void OnBuilt(DistributedApplication application)
    {
        App = application;
        base.OnBuilt(application);
    }

    protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Services.AddLogging(builder =>
            {
                builder.AddXUnit();
                if (Environment.GetEnvironmentVariable("RUNNER_DEBUG") is not null or "1")
                    builder.SetMinimumLevel(LogLevel.Trace);
                else
                    builder.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        base.OnBuilderCreated(applicationBuilder);
    }

    public Task InitializeAsync() => StartAsync();

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}