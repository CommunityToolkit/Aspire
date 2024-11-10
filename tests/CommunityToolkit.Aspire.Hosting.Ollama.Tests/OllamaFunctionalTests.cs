// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
using Aspire.Hosting.Utils;
using OllamaSharp;
namespace CommunityToolkit.Aspire.Hosting.Ollama.Tests;

[RequiresDocker]
public class OllamaFunctionalTests(ITestOutputHelper testOutputHelper)
{
    private const string model = "tinyllama";

    [Fact]
    public async Task VerifyOllamaResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var ollama = builder.AddOllama("ollama");

        using var app = builder.Build();

        await app.StartAsync();

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceAsync(ollama.Resource.Name, KnownResourceStates.Running);

        var hb = Host.CreateApplicationBuilder();

        hb.Configuration[$"ConnectionStrings:{ollama.Resource.Name}"] = await ollama.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddOllamaApiClient(ollama.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        var ollamaApi = host.Services.GetRequiredService<IOllamaApiClient>();

        var models = await ollamaApi.ListLocalModelsAsync();
        Assert.Empty(models);
    }

    [Fact]
    public async Task AddModelShouldDownloadModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var ollama = builder.AddOllama("ollama");
        var tinyllama = ollama.AddModel(model, model);

        using var app = builder.Build();

        await app.StartAsync();

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceAsync(ollama.Resource.Name, KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));

        await rns.WaitForResourceAsync(tinyllama.Resource.Name, KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));

        var hb = Host.CreateApplicationBuilder();

        hb.Configuration[$"ConnectionStrings:{ollama.Resource.Name}"] = await ollama.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddOllamaApiClient(ollama.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        var ollamaApi = host.Services.GetRequiredService<IOllamaApiClient>();

        var models = await ollamaApi.ListLocalModelsAsync();

        Assert.Single(models);
        Assert.StartsWith(model, models.First().Name);
    }

    [Fact]
    public async Task WithDataShouldPersistStateBetweenUsages()
    {
        string? volumeName = null;
        try
        {
            using var builder1 = TestDistributedApplicationBuilder.Create(testOutputHelper);

            var ollama1 = builder1.AddOllama("ollama");
            var model1 = ollama1.AddModel(model, model);

            // Use a deterministic volume name to prevent them from exhausting the machines if deletion fails
#pragma warning disable CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            volumeName = VolumeNameGenerator.CreateVolumeName(ollama1, nameof(WithDataShouldPersistStateBetweenUsages));
#pragma warning restore CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            // if the volume already exists (because of a crashing previous run), delete it
            DockerUtils.AttemptDeleteDockerVolume(volumeName, throwOnFailure: true);
            ollama1.WithDataVolume(volumeName);

            using (var app = builder1.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();

                await rns.WaitForResourceAsync(ollama1.Resource.Name, KnownResourceStates.Running);
                await rns.WaitForResourceHealthyAsync(model1.Resource.Name);

                try
                {
                    var hb = Host.CreateApplicationBuilder();

                    hb.Configuration[$"ConnectionStrings:{ollama1.Resource.Name}"] = await ollama1.Resource.ConnectionStringExpression.GetValueAsync(default);

                    hb.AddOllamaApiClient(ollama1.Resource.Name);

                    using var host = hb.Build();
                    await host.StartAsync();

                    var ollamaApiClient = host.Services.GetRequiredService<IOllamaApiClient>();
                    var models = await ollamaApiClient.ListLocalModelsAsync();
                    Assert.Single(models);
                    Assert.StartsWith(model, models.First().Name);
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync();
                }
            }

            using var builder2 = TestDistributedApplicationBuilder.Create(testOutputHelper);
            var ollama2 = builder2.AddOllama("ollama")
                .WithDataVolume(volumeName);
            var model2 = ollama2.AddModel(model, model);

            using (var app = builder2.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();

                await rns.WaitForResourceAsync(ollama2.Resource.Name, KnownResourceStates.Running);
                await rns.WaitForResourceHealthyAsync(model2.Resource.Name);

                try
                {
                    var hb = Host.CreateApplicationBuilder();

                    hb.Configuration[$"ConnectionStrings:{ollama2.Resource.Name}"] = await ollama2.Resource.ConnectionStringExpression.GetValueAsync(default);

                    hb.AddOllamaApiClient(ollama2.Resource.Name);

                    using var host = hb.Build();
                    await host.StartAsync();
                    var ollamaApiClient = host.Services.GetRequiredService<IOllamaApiClient>();
                    var models = await ollamaApiClient.ListLocalModelsAsync();
                    Assert.Single(models);
                    Assert.StartsWith(model, models.First().Name);
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync();
                }
            }
        }
        finally
        {
            if (volumeName is not null)
            {
                DockerUtils.AttemptDeleteDockerVolume(volumeName);
            }
        }
    }

    [Fact]
    public async Task ModelInitiallyUnhealthy()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var ollama = builder.AddOllama("ollama");
        var tinyllama = ollama.AddModel(model, model);

        using var app = builder.Build();

        await app.StartAsync();

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        var resourceEvent = await rns.WaitForResourceAsync(tinyllama.Resource.Name, re => re.Snapshot.HealthStatus == HealthStatus.Unhealthy).WaitAsync(TimeSpan.FromMinutes(5));

        Assert.Equal(HealthStatus.Unhealthy, resourceEvent.Snapshot.HealthStatus);

        await rns.WaitForResourceAsync(ollama.Resource.Name, KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));

        await rns.WaitForResourceAsync(tinyllama.Resource.Name, KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));

        var hb = Host.CreateApplicationBuilder();

        hb.Configuration[$"ConnectionStrings:{ollama.Resource.Name}"] = await ollama.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddOllamaApiClient(ollama.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        var ollamaApi = host.Services.GetRequiredService<IOllamaApiClient>();

        var models = await ollamaApi.ListLocalModelsAsync();

        Assert.Single(models);
        Assert.StartsWith(model, models.First().Name);
    }
}
