using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Microsoft.Extensions.Hosting;
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

    protected override void OnBuilderCreating(DistributedApplicationOptions applicationOptions, HostApplicationBuilderSettings hostOptions)
    {
        // In CI we use a custom container registry to pull images from so that we can avoid hitting the rate limit on Docker Hub
        // see: https://github.com/CommunityToolkit/Aspire/issues/556
        if (Environment.GetEnvironmentVariable("CUSTOM_CONTAINER_REGISTRY") is not null)
        {
            applicationOptions.ContainerRegistryOverride = Environment.GetEnvironmentVariable("CUSTOM_CONTAINER_REGISTRY");
        }

        base.OnBuilderCreating(applicationOptions, hostOptions);
    }

    public async override ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        catch (Exception)
        {
            // Ignore exceptions during disposal
        }
    }

    public Task InitializeAsync() => StartAsync().WaitAsync(TimeSpan.FromMinutes(10));

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}