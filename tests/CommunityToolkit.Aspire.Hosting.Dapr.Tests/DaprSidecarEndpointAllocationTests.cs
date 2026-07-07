// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using System.Runtime.CompilerServices;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

public class DaprSidecarEndpointAllocationTests
{
    // Regression test for microsoft/aspire#18242: the Dapr env callback must not eagerly read the
    // sidecar CLI endpoint Port before allocation. GetResourceDependenciesAsync(DirectOnly) is the
    // operation AzureResourcePreparer runs per compute resource, and reproduces the crash without Azure.
    [Fact]
    public async Task AppEnvironmentCallback_DoesNotThrow_WhenDaprSidecarEndpointsNotAllocated()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        // Fake path so the lifecycle hook does not fail locating the Dapr CLI.
        builder.AddDapr(o => o.DaprPath = "dapr");

        var container = builder.AddContainer("worker", "image")
            .WithDaprSidecar();

        using var app = builder.Build();

        // Runs the Dapr hook (creates "worker-dapr-cli" and attaches the callback); endpoints stay unallocated.
        await ExecuteBeforeStartHooksAsync(app, default);

        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);

        var dependencies = await container.Resource.GetResourceDependenciesAsync(
            executionContext,
            ResourceDependencyDiscoveryMode.DirectOnly);

        Assert.Contains(dependencies, r => r.Name == "worker-dapr-cli");

        // Fire the callback directly (do not resolve - a deferred port blocks until allocation) and
        // assert the ports are recorded as deferred providers rather than eagerly-read values.
        var callbackContext = new EnvironmentCallbackContext(executionContext, container.Resource);
        foreach (var callback in container.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(callbackContext);
        }

        Assert.True(callbackContext.EnvironmentVariables.ContainsKey("DAPR_HTTP_PORT"));
        Assert.True(callbackContext.EnvironmentVariables.ContainsKey("DAPR_GRPC_PORT"));
        Assert.IsAssignableFrom<IValueProvider>(callbackContext.EnvironmentVariables["DAPR_HTTP_PORT"]);
        Assert.IsAssignableFrom<IValueProvider>(callbackContext.EnvironmentVariables["DAPR_GRPC_PORT"]);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);
}
