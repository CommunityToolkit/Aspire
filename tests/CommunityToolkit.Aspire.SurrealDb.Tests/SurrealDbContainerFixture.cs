// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CommunityToolkit.Aspire.SurrealDb.Tests;

public sealed class SurrealDbContainerFixture : IAsyncLifetime
{
    private const string _username = "root";
    private string _password = string.Empty;
    private const int _port = 8000;
    
    public IContainer? Container { get; private set; }
    
    public string GetConnectionString()
    {
        if (Container is null)
        {
            throw new InvalidOperationException("The test container was not initialized.");
        }
        
        var endpoint = new UriBuilder("ws", Container.Hostname, Container.GetMappedPublicPort(_port), "/rpc").ToString();
        return $"Endpoint={endpoint};Username={_username};Password={_password}";
    }
    
    public async Task InitializeAsync()
    {
        if (RequiresDockerAttribute.IsSupported)
        {
            var paramGenerator = new GenerateParameterDefault
            {
                MinLength = 8,
                Lower = true,
                Upper = true,
                Numeric = true,
                Special = false,
                MinLower = 1,
                MinUpper = 1,
                MinNumeric = 1,
                MinSpecial = 0
            };

            _password = paramGenerator.GetDefaultValue();

            Container = new ContainerBuilder()
                .WithImage($"{SurrealDbContainerImageTags.Registry}/{SurrealDbContainerImageTags.Image}:{SurrealDbContainerImageTags.Tag}")
                .WithPortBinding(_port, true)
                .WithWaitStrategy(Wait
                    .ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(
                        r => r.ForPort(_port)
                              .ForStatusCodeMatching(code => (int)code is >= 200 and <= 399), static modifier => modifier.WithTimeout(TimeSpan.FromMinutes(1))))
                .WithEnvironment("SURREAL_USER", _username)
                .WithEnvironment("SURREAL_PASS", _password)
                .WithCommand("start", "memory")
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