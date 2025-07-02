using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Hosting.Minio;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CommunityToolkit.Aspire.Minio.Client.Tests;

public class MinioContainerFeature : IAsyncLifetime
{
    private const int MinioPort = 9000;
    public IContainer? Container { get; private set; }
    public string GetContainerEndpoint()
    {
        if (Container is null)
        {
            throw new InvalidOperationException("The test container was not initialized.");
        }
        var endpoint = new UriBuilder("http", Container.Hostname, Container.GetMappedPublicPort(MinioPort)).ToString();
        return endpoint;
    }

    public async Task InitializeAsync()
    {
        if (RequiresDockerAttribute.IsSupported)
        {
            Container = new ContainerBuilder()
              .WithImage($"{MinioContainerImageTags.Registry}/{MinioContainerImageTags.Image}:{MinioContainerImageTags.Tag}")
              .WithPortBinding(MinioPort, true)
              .WithCommand("server", "/data")
              .WithEnvironment("MINIO_ADDRESS", $":{9000.ToString()}")
              .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
              .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
              .WithWaitStrategy(Wait.ForUnixContainer()
                  .UntilHttpRequestIsSucceeded(request => request.ForPath("/minio/health/ready")
                      .ForPort(MinioPort)))
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
