using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Projects;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

[RequiresDocker]
public class FunctionalTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task VerifyPublishSqlProjectWaitForDependentResources()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var healthCheckTcs = new TaskCompletionSource<HealthCheckResult>();
        builder.Services.AddHealthChecks().AddAsyncCheck("blocking_check", () =>
        {
            return healthCheckTcs.Task;
        });

        var resource = builder.AddSqlServer("resource")
                              .WithHealthCheck("blocking_check");

        var database = resource.AddDatabase("TargetDatabase");

        var otherDatabase = resource.AddDatabase("OtherTargetDatabase");

        var dependentResource = builder.AddSqlProject<Projects.SdkProject>("dependentresource")
                                       .WithReference(database);

        var otherDependentResource = builder.AddSqlProject<Projects.SdkProject>("other-sdk-project")
       .WithReference(otherDatabase)
       .WaitForCompletion(dependentResource);

        using var app = builder.Build();

        var pendingStart = app.StartAsync(cts.Token);

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceAsync(resource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, "Pending", cts.Token);

        await rns.WaitForResourceAsync(otherDependentResource.Resource.Name, "Pending", cts.Token);


        healthCheckTcs.SetResult(HealthCheckResult.Healthy());

        await rns.WaitForResourceAsync(resource.Resource.Name, re => re.Snapshot.HealthStatus == HealthStatus.Healthy, cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, "Publishing", cts.Token);

        await rns.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Finished, cts.Token);

        await rns.WaitForResourceAsync(otherDependentResource.Resource.Name, "Publishing", cts.Token);

        await rns.WaitForResourceAsync(otherDependentResource.Resource.Name, KnownResourceStates.Finished, cts.Token);

        await pendingStart;

        await app.StopAsync();
    }
}
