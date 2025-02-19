// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
using Aspire.Hosting.Utils;
using OpenFeature.Contrib.Providers.GOFeatureFlag;
using OpenFeature.Model;

namespace CommunityToolkit.Aspire.Hosting.GoFeatureFlag.Tests;

[RequiresDocker]
public class GoFeatureFlagFunctionalTests(ITestOutputHelper testOutputHelper)
{
    private static readonly string SOURCE = Path.GetFullPath("./goff", Directory.GetCurrentDirectory());
    
    [Fact]
    public async Task VerifyGoFeatureFlagResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var goff = builder.AddGoFeatureFlag("goff")
            .WithBindMount(SOURCE, "/goff");

        using var app = builder.Build();

        await app.StartAsync();

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceHealthyAsync(goff.Resource.Name);
        
        var hb = Host.CreateApplicationBuilder();

        hb.Configuration[$"ConnectionStrings:{goff.Resource.Name}"] = await goff.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddGoFeatureFlagClient(goff.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        var goFeatureFlagProvider = host.Services.GetRequiredService<GoFeatureFlagProvider>();

        await VerifyTestData(goFeatureFlagProvider);
    }

    [Fact]
    public async Task VerifyWaitForOnGoFeatureFlagBlocksDependentResources()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var healthCheckTcs = new TaskCompletionSource<HealthCheckResult>();
        builder.Services.AddHealthChecks().AddAsyncCheck("blocking_check", () =>
        {
            return healthCheckTcs.Task;
        });

        var resource = builder.AddGoFeatureFlag("resource")
            .WithBindMount(SOURCE, "/goff")
            .WithHealthCheck("blocking_check");

        var dependentResource = builder.AddGoFeatureFlag("dependentresource")
            .WithBindMount(SOURCE, "/goff")
            .WaitFor(resource);

        using var app = builder.Build();

        var pendingStart = app.StartAsync(cts.Token);

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceAsync(resource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Waiting, cts.Token);

        healthCheckTcs.SetResult(HealthCheckResult.Healthy());

        await rns.WaitForResourceAsync(resource.Resource.Name, re => re.Snapshot.HealthStatus == HealthStatus.Healthy, cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await pendingStart;

        await app.StopAsync();
    }

    private static async Task VerifyTestData(GoFeatureFlagProvider goFeatureFlagProvider)
    {
        var userContext = EvaluationContext.Builder()
            .Set("targetingKey", Guid.NewGuid().ToString())
            .Set("anonymous", true)
            .Build();
        string featureName = "display-banner";
        var flag = await goFeatureFlagProvider.ResolveBooleanValueAsync(
            featureName, false, userContext
        );

        Assert.NotNull(flag);
        Assert.True(flag.Value);
    }
}