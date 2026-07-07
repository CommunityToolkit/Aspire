// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Dapr;
using System.Runtime.CompilerServices;

namespace CommunityToolkit.Aspire.Hosting.Azure.Dapr.Tests;

public class DaprAzureContainerAppEnvironmentTests
{
    // End-to-end guard for microsoft/aspire#18242 in the topology that triggers it:
    // AddAzureContainerAppEnvironment (which contributes the azure-prepare-resources walk) plus a
    // Dapr sidecar. Invokes the preparer's core operation directly so the test needs no Docker or
    // provisioning; the non-Azure Dapr.Tests test is the primary deterministic guard.
    [Fact]
    public async Task PrepareResourcesWalk_DoesNotThrow_WithAzureEnvironmentAndDaprSidecar()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        // Fake path so the Dapr lifecycle hook does not fail locating the Dapr CLI.
        builder.AddDapr(o => o.DaprPath = "dapr");
        builder.AddAzureContainerAppEnvironment("cae");

        var worker = builder.AddContainer("worker", "image")
            .WithDaprSidecar();

        using var app = builder.Build();

        await ExecuteBeforeStartHooksAsync(app, default);

        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);

        var exception = await Record.ExceptionAsync(() =>
            worker.Resource.GetResourceDependenciesAsync(executionContext, ResourceDependencyDiscoveryMode.DirectOnly));

        Assert.Null(exception);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);
}
