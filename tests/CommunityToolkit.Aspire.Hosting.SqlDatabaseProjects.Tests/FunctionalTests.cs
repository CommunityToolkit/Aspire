using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

[RequiresDocker]
public class FunctionalTests(ITestOutputHelper testOutputHelper, SqlServerContainerFixture sqlServerContainerFixture) : IClassFixture<SqlServerContainerFixture>
{
    [Theory]
    [InlineData(TestProject.RelativePath, "SdkProject")]
    [InlineData("../../../../../examples/sql-database-projects/MicrosoftBuildSqlProject/MicrosoftBuildSqlProject.sqlproj", "MicrosoftBuildSqlProject")]
    public async Task VerifyProjectPathDeployment(string relativeProjectPath, string tableName)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMinutes(5));
        var connectionString = new SqlConnectionStringBuilder(sqlServerContainerFixture.GetConnectionString())
        {
            InitialCatalog = $"ProjectPath_{Guid.NewGuid():N}"
        }.ConnectionString;

        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        builder.Configuration["ConnectionStrings:Target"] = connectionString;
        var target = builder.AddConnectionString("Target");
        var projectPath = Path.GetFullPath(relativeProjectPath, AppContext.BaseDirectory);
        var project = builder.AddSqlProject("schema", projectPath).WithReference(target);

        await using var app = builder.Build();
        await app.StartAsync(cancellation.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync(project.Resource.Name, KnownResourceStates.Finished, cancellation.Token);

        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellation.Token);
        await using var command = sqlConnection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName";
        command.Parameters.AddWithValue("@tableName", tableName);
        Assert.Equal(1, await command.ExecuteScalarAsync(cancellation.Token));

        await app.StopAsync(cancellation.Token);
    }

    [Fact]
    public async Task VerifyPackageExplicitStartAndRepeatedCommandDeployment()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMinutes(5));
        var appHostDirectory = Path.Combine(Path.GetTempPath(), $"sql-package-deploy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(appHostDirectory);

        try
        {
            using var builder = TestDistributedApplicationBuilder.Create(options => options.ProjectDirectory = appHostDirectory, testOutputHelper);
            var deployer = new CountingDacpacDeployer();
            builder.Services.AddSingleton<IDacpacDeployer>(deployer);
            var artifact = builder.AddSqlProject<TestProject>("artifact");
            var dacpacPath = ((IResourceWithDacpac)artifact.Resource).GetDacpacPath();
            var packageId = "Company.Database";
            var relativePath = Path.Combine("schema", "custom.dacpac");
            var feedDirectory = Path.Combine(appHostDirectory, "feed");
            AddSqlPackageTests.CreateNuGetPackage(feedDirectory, packageId, "1.2.3-preview.1", relativePath, dacpacPath);
            AddSqlPackageTests.CreateNuGetConfig(appHostDirectory, feedDirectory, Path.Combine(appHostDirectory, "packages"));
            var connectionString = new SqlConnectionStringBuilder(sqlServerContainerFixture.GetConnectionString())
            {
                InitialCatalog = $"Package_{Guid.NewGuid():N}"
            }.ConnectionString;
            builder.Configuration["ConnectionStrings:Target"] = connectionString;
            var target = builder.AddConnectionString("Target");
            var package = builder.AddSqlPackage("schema", packageId, "1.2.3-preview.1")
                .WithDacpac(relativePath)
                .WithSkipWhenDeployed()
                .WithReference(target)
                .WithExplicitStart();

            await using var app = builder.Build();
            await app.StartAsync(cancellation.Token);
            Assert.Null(package.Resource.DacpacPath);

            var deployCommand = Assert.Single(package.Resource.Annotations.OfType<ResourceCommandAnnotation>(), command => command.Name == "deploy");
            var commandContext = new ExecuteCommandContext
            {
                ResourceName = package.Resource.Name,
                Services = app.Services,
                Logger = app.Services.GetRequiredService<ResourceLoggerService>().GetLogger(package.Resource),
                Arguments = new InteractionInputCollection([]),
                CancellationToken = cancellation.Token
            };
            Assert.True((await deployCommand.ExecuteCommand(commandContext)).Success);
            Assert.True((await deployCommand.ExecuteCommand(commandContext)).Success);
            Assert.Equal(1, deployer.DeployCount);

            await using var sqlConnection = new SqlConnection(connectionString);
            await sqlConnection.OpenAsync(cancellation.Token);
            await using var query = sqlConnection.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'SdkProject'";
            Assert.Equal(1, await query.ExecuteScalarAsync(cancellation.Token));
            Assert.True(package.Resource.TryGetLastAnnotation(out SqlPackageRestoreAnnotation? input));
            Assert.Equal(relativePath, input.DacpacRelativePath);

            await app.StopAsync(cancellation.Token);
        }
        finally
        {
            Directory.Delete(appHostDirectory, recursive: true);
        }
    }

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

        var dependentResource = builder.AddSqlProject<TestProject>("dependentresource")
                                       .WithReference(database);

        var otherDependentResource = builder.AddSqlProject<TestProject>("other-sdk-project")
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

        var sdkProject = builder.AddSqlProject<TestProject>("sdkProject")
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

    private sealed class CountingDacpacDeployer : IDacpacDeployer
    {
        private readonly DacpacDeployer deployer = new();

        public int DeployCount { get; private set; }

        public void Deploy(string dacpacPath, DacDeployOptions options, string targetConnectionString, string? targetDatabaseName, ILogger deploymentLogger, CancellationToken cancellationToken)
        {
            DeployCount++;
            deployer.Deploy(dacpacPath, options, targetConnectionString, targetDatabaseName, deploymentLogger, cancellationToken);
        }
    }
}
