// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Hosting.InfluxDB;
using Aspire.Components.Common.Tests;
using Aspire.Hosting.Utils;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace CommunityToolkit.Aspire.InfluxDB.Tests;

public sealed class InfluxDBContainerFixture : IAsyncLifetime
{
    public IContainer? Container { get; private set; }
    private string _token = string.Empty;
    private string _username = "admin";
    private string _password = string.Empty;

    public string GetConnectionString()
    {
        if (Container is null)
        {
            throw new InvalidOperationException("The test container was not initialized.");
        }
        var endpoint = new UriBuilder("http", Container.Hostname, Container.GetMappedPublicPort(8086)).ToString();
        return $"Url={endpoint};Token={_token};Organization=default;Bucket=default";
    }

    public async Task InitializeAsync()
    {
        if (RequiresDockerAttribute.IsSupported)
        {
            var param = new GenerateParameterDefault
            {
                MinLength = 32,
                Lower = true,
                Upper = true,
                Numeric = true,
                Special = false,
                MinLower = 1,
                MinUpper = 1,
                MinNumeric = 1,
                MinSpecial = 0
            };
            _token = param.GetDefaultValue();
            _password = param.GetDefaultValue();

            Container = new ContainerBuilder()
              .WithImage($"{InfluxDBContainerImageTags.Registry}/{InfluxDBContainerImageTags.Image}:{InfluxDBContainerImageTags.Tag}")
              .WithPortBinding(8086, true)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8086).ForPath("/health")))
              .WithEnvironment("DOCKER_INFLUXDB_INIT_MODE", "setup")
              .WithEnvironment("DOCKER_INFLUXDB_INIT_USERNAME", _username)
              .WithEnvironment("DOCKER_INFLUXDB_INIT_PASSWORD", _password)
              .WithEnvironment("DOCKER_INFLUXDB_INIT_ORG", "default")
              .WithEnvironment("DOCKER_INFLUXDB_INIT_BUCKET", "default")
              .WithEnvironment("DOCKER_INFLUXDB_INIT_ADMIN_TOKEN", _token)
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
