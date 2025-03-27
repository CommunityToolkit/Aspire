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

        if (Environment.GetEnvironmentVariable("CUSTOM_CONTAINER_REGISTRY") is not null)
        {
            // We can't use the built-in Aspire container override feature because that changes the registry for all
            // containers, and we only want to change the registry for the default Docker Hub registry. So we need to
            // override the registry in the BeforeStartEvent handler (essentially implementing a subset of the built-in
            // functionality ourselves).
            applicationBuilder.Eventing.Subscribe<BeforeStartEvent>((@event, ct) =>
            {
                var resourcesWithContainerImages = @event.Model.Resources.SelectMany(
                    r => r.Annotations.OfType<ContainerImageAnnotation>()
                                      .Select(cia => new { Resource = r, Annotation = cia })
                    );

                foreach (var resourceWithContainerImage in resourcesWithContainerImages)
                {
                    string? dockerHubOverride = Environment.GetEnvironmentVariable("CUSTOM_CONTAINER_REGISTRY");

                    // We only override the registry if the resource is using the default Docker Hub registry, as that is
                    // rate limited. Microsoft Artifact Registry and GitHub Container Registry are not rate limited, or at
                    // least we don't pull enough images to hit the rate limit.
                    if (dockerHubOverride is not null && resourceWithContainerImage.Annotation.Registry == "docker.io")
                    {
                        resourceWithContainerImage.Annotation.Registry = dockerHubOverride;
                    }
                }

                return Task.CompletedTask;
            });
        }

        base.OnBuilderCreated(applicationBuilder);
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