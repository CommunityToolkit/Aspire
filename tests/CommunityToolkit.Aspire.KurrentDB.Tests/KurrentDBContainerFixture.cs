// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Hosting.KurrentDB;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CommunityToolkit.Aspire.KurrentDB.Tests;

public sealed class KurrentDBContainerFixture : IAsyncLifetime
{
    public IContainer? Container { get; private set; }

    public string GetConnectionString()
    {
        if (Container is null)
        {
            throw new InvalidOperationException("The test container was not initialized.");
        }
        var endpoint = new UriBuilder("esdb", Container.Hostname, Container.GetMappedPublicPort(2113)).ToString();
        return $"{endpoint}?tls=false";
    }

    public async Task InitializeAsync()
    {
        if (RequiresDockerAttribute.IsSupported)
        {
            Container = new ContainerBuilder()
              .WithImage($"{KurrentDBContainerImageTags.Registry}/{KurrentDBContainerImageTags.Image}:{KurrentDBContainerImageTags.Tag}")
              .WithPortBinding(2113, true)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(2113)))
              .WithEnvironment("EVENTSTORE_CLUSTER_SIZE", "1")
              .WithEnvironment("EVENTSTORE_NODE_PORT", "2113")
              .WithEnvironment("EVENTSTORE_INSECURE", "true")
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
