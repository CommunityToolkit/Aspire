using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

[RequiresDocker]
public class FunctionalTests(ITestOutputHelper testOutputHelper, SqlServerContainerFixture sqlServerContainerFixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact(Skip = "Disabling at Aspire 9.1 changes some of how dependency waiting works so test needs to be re-evaluated")]
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

    [Fact]
    public async Task VerifyWithConnectionStringReference()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        var connectionString = sqlServerContainerFixture.GetConnectionString();

        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        builder.Configuration["ConnectionStrings:Aspire"] = connectionString;

        var connection = builder.AddConnectionString("Aspire");

        var sdkProject = builder.AddSqlProject<Projects.SdkProject>("sdkProject")
        .WithReference(connection);

        var app = builder.Build();

        var pendingStart = app.StartAsync(cts.Token);

        var rns = app.Services.GetRequiredService<ResourceNotificationService>();

        await rns.WaitForResourceAsync(sdkProject.Resource.Name, KnownResourceStates.Finished).WaitAsync(TimeSpan.FromMinutes(1));

        using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync();

        using var command = sqlConnection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) " +
            "FROM   INFORMATION_SCHEMA.TABLES " +
            "WHERE  TABLE_SCHEMA = 'dbo' " +
           $"AND    TABLE_NAME = 'SdkProject'";

        var result = await command.ExecuteScalarAsync();
        Assert.Equal(1, result);

        await pendingStart;

        await app.StopAsync();
    }
}
