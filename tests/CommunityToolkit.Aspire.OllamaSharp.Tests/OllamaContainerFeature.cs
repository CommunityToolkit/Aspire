using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Hosting.Ollama;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CommunityToolkit.Aspire.OllamaSharp.Tests;

public class OllamaContainerFeature : IAsyncLifetime
{
    public IContainer? Container { get; private set; }
    public string GetConnectionString()
    {
        if (Container is null)
        {
            throw new InvalidOperationException("The test container was not initialized.");
        }
        var endpoint = new UriBuilder("http", Container.Hostname, Container.GetMappedPublicPort(11434)).ToString();
        return endpoint;
    }

    public async Task InitializeAsync()
    {
        if (RequiresDockerAttribute.IsSupported)
        {
            Container = new ContainerBuilder()
              .WithImage($"{OllamaContainerImageTags.Registry}/{OllamaContainerImageTags.Image}:{OllamaContainerImageTags.Tag}")
              .WithPortBinding(11434, true)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(11434)))
              .Build();

            await Container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }
}
