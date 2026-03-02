// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Hosting.GoFeatureFlag;
using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.GoFeatureFlag.Tests;

public sealed class GoFeatureFlagContainerFixture : IAsyncLifetime
{
    public IContainer? Container { get; private set; }
    public string GetConnectionString()
    {
        if (Container is null)
        {
            throw new InvalidOperationException("The test container was not initialized.");
        }
        var endpoint = new UriBuilder("http", Container.Hostname, Container.GetMappedPublicPort(1031)).ToString();
        return $"Endpoint={endpoint}";
    }

    public async ValueTask InitializeAsync()
    {
        if (RequiresDockerAttribute.IsSupported)
        {
            var source = Path.GetFullPath("./goff", Directory.GetCurrentDirectory());
            Container = new ContainerBuilder()
              .WithImage($"{GoFeatureFlagContainerImageTags.Registry}/{GoFeatureFlagContainerImageTags.Image}:{GoFeatureFlagContainerImageTags.Tag}")
              .WithPortBinding(1031, true)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(1031)))
              .WithBindMount(source, "/goff")
              .Build();

            await Container.StartAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }
}
