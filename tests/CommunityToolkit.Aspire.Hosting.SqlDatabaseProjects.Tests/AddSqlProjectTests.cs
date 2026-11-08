using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class AddSqlProjectTests
{
    [Fact]
    public void AddSqlProject_WithProjectMetadata()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlProject<TestProject>("MySqlProject");

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        var dacpacPath = ((IResourceWithDacpac)sqlProjectResource).GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.True(File.Exists(dacpacPath), $"Dacpac file not found at '{dacpacPath}'");
    }

    [Fact]
    public void AddSqlProject_WithExplicitPath()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlProject("MySqlProject").WithDacpac(TestProject.RelativePath);

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.TryGetLastAnnotation(out DacpacMetadataAnnotation? dacpacMetadataAnnotation));
        Assert.Equal(TestProject.RelativePath, dacpacMetadataAnnotation.DacpacPath);

        var dacpacPath = ((IResourceWithDacpac)sqlProjectResource).GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.True(File.Exists(dacpacPath));
    }

    [Fact]
    public async Task AddSqlProject_FromPathBuildsBeforeDeploying()
    {
        var projectPath = Path.GetFullPath(TestProject.RelativePath, AppContext.BaseDirectory);
        var deployer = new RecordingDacpacDeployer();
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["ConnectionStrings:Target"] = "Server=localhost;Database=Target;Integrated Security=True;TrustServerCertificate=True";
        appBuilder.Services.AddSingleton<IDacpacDeployer>(deployer);

        var project = appBuilder.AddSqlProject("MySqlProject", projectPath, "Release");
        var target = appBuilder.AddConnectionString("Target");
        project.WithReference(target);
        var expectedDacpacPath = ((IResourceWithDacpac)project.Resource).GetDacpacPath();

        using var app = appBuilder.Build();
        var publishService = app.Services.GetRequiredService<SqlProjectPublishService>();

        await publishService.PublishSqlProject(project.Resource, target.Resource, null, CancellationToken.None);

        Assert.NotNull(deployer.DacpacPath);
        Assert.True(deployer.DacpacExistedDuringDeploy);
        Assert.False(File.Exists(deployer.DacpacPath));
        Assert.Contains("Release", expectedDacpacPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSqlProject_ConcurrentBuildsForSamePathUseIsolatedDacpacs()
    {
        var projectPath = Path.GetFullPath(TestProject.RelativePath, AppContext.BaseDirectory);
        var deployer = new BlockingDacpacDeployer();
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:Target"] = "Server=localhost;Database=Target;Integrated Security=True;TrustServerCertificate=True";
        builder.Services.AddSingleton<IDacpacDeployer>(deployer);
        var target = builder.AddConnectionString("Target");
        var firstProject = builder.AddSqlProject("first-schema", projectPath).WithReference(target);
        var secondProject = builder.AddSqlProject("second-schema", projectPath).WithReference(target);

        using var app = builder.Build();
        var publishService = app.Services.GetRequiredService<SqlProjectPublishService>();
        var firstDeployment = Task.Run(() => publishService.PublishSqlProject(firstProject.Resource, target.Resource, null, CancellationToken.None));
        var secondDeployment = Task.Run(() => publishService.PublishSqlProject(secondProject.Resource, target.Resource, null, CancellationToken.None));

        try
        {
            await deployer.WaitForTwoDeploymentsAsync(TimeSpan.FromMinutes(2));

            var dacpacPaths = deployer.DacpacPaths;
            Assert.Equal(2, dacpacPaths.Length);
            Assert.NotEqual(dacpacPaths[0], dacpacPaths[1]);
            Assert.All(dacpacPaths, path => Assert.True(File.Exists(path)));
        }
        finally
        {
            deployer.ReleaseDeployments();
            await Task.WhenAll(firstDeployment, secondDeployment);
        }

        Assert.All(deployer.DacpacPaths, path => Assert.False(File.Exists(path)));
    }

    [Theory]
    [InlineData("missing-project")]
    [InlineData("failed-build")]
    [InlineData("missing-dacpac")]
    [InlineData("cancelled")]
    public async Task AddSqlProject_PreparationFailurePreventsDeployment(string failure)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql project error {Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Database.sqlproj");
            var dacpacPath = Path.Combine(directory, "Database.dacpac");
            if (failure != "missing-dacpac")
            {
                await File.WriteAllBytesAsync(dacpacPath, [1, 2, 3]);
            }

            if (failure != "missing-project")
            {
                var projectDocument = new XDocument(
                    new XElement("Project",
                        new XAttribute("DefaultTargets", "Build"),
                        new XElement("PropertyGroup", new XElement("SqlTargetPath", dacpacPath)),
                        new XElement("Target", new XAttribute("Name", "Restore")),
                        new XElement("Target", new XAttribute("Name", "Build"),
                            failure == "failed-build" ? new XElement("Error", new XAttribute("Text", "Intentional test build failure")) : null)));
                projectDocument.Save(projectPath);
            }

            var deployer = new RecordingDacpacDeployer();
            var builder = DistributedApplication.CreateBuilder();
            builder.Configuration["ConnectionStrings:Target"] = "Server=localhost;Database=Target;Integrated Security=True";
            builder.Services.AddSingleton<IDacpacDeployer>(deployer);
            var target = builder.AddConnectionString("Target");
            var project = builder.AddSqlProject("schema", projectPath).WithReference(target);
            using var app = builder.Build();
            using var cancellation = new CancellationTokenSource();
            if (failure == "cancelled")
            {
                cancellation.Cancel();
            }

            await app.Services.GetRequiredService<SqlProjectPublishService>()
                .PublishSqlProject(project.Resource, target.Resource, null, cancellation.Token);

            Assert.Null(deployer.DacpacPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AddSqlProject_WithoutDeploymentOptions()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlProject("MySqlProject");

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        Assert.False(sqlProjectResource.TryGetLastAnnotation(out ConfigureDacDeployOptionsAnnotation? _));

        var options = ((IResourceWithDacpac)sqlProjectResource).GetDacpacDeployOptions();
        Assert.NotNull(options);
        Assert.Equivalent(new DacDeployOptions(), options);
    }

    [Fact]
    public void AddSqlProject_WithDeploymentOptions()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        Action<DacDeployOptions> configureAction = options => options.IncludeCompositeObjects = true;

        appBuilder.AddSqlProject("MySqlProject").WithConfigureDacDeployOptions(configureAction);

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.TryGetLastAnnotation(out ConfigureDacDeployOptionsAnnotation? configureDacDeployOptionsAnnotation));
        Assert.Same(configureAction, configureDacDeployOptionsAnnotation.ConfigureDeploymentOptions);

        var options = ((IResourceWithDacpac)sqlProjectResource).GetDacpacDeployOptions();
        Assert.True(options.IncludeCompositeObjects);
    }

    [Fact]
    public void AddSqlProject_WithDeploymentOptions_FromFile_NonExisting()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        var optionsPath = "/folder/project.publish.xml";

        appBuilder.AddSqlProject("MySqlProject").WithDacDeployOptions(optionsPath);

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.TryGetLastAnnotation(out DacDeployOptionsAnnotation? dacDeployOptionsAnnotation));
        Assert.Equal(optionsPath, dacDeployOptionsAnnotation.OptionsPath);

        Assert.Throws<DirectoryNotFoundException>(() => ((IResourceWithDacpac)sqlProjectResource).GetDacpacDeployOptions());
    }

    [Fact]
    public void AddSqlProject_WithDeploymentOptions_FromFile()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        var optionsPath = "Database.publish.xml";

        appBuilder.AddSqlProject("MySqlProject").WithDacDeployOptions(optionsPath);

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.TryGetLastAnnotation(out DacDeployOptionsAnnotation? dacDeployOptionsAnnotation));
        Assert.Equal(optionsPath, dacDeployOptionsAnnotation.OptionsPath);

        var options = ((IResourceWithDacpac)sqlProjectResource).GetDacpacDeployOptions();
        Assert.False(options.BlockOnPossibleDataLoss);
    }

    [Fact]
    public void WithReference_AddsRequiredServices()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var targetDatabase = appBuilder.AddSqlServer("sql").AddDatabase("test");
        appBuilder.AddSqlProject<TestProject>("MySqlProject")
                  .WithReference(targetDatabase);

        // Act
        using var app = appBuilder.Build();

        // Assert
        Assert.Single(app.Services.GetServices<SqlProjectPublishService>());
        Assert.Single(app.Services.GetServices<IDacpacDeployer>());
        Assert.Single(app.Services.GetServices<IDacpacChecksumService>());
    }

    [Fact]
    public void AddSqlProject_WithExplicitStart()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var targetDatabase = appBuilder.AddSqlServer("sql").AddDatabase("test");
        appBuilder.AddSqlProject<TestProject>("MySqlProject")
            .WithReference(targetDatabase)
            .WithExplicitStart();

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.HasAnnotationOfType<ExplicitStartupAnnotation>());
    }

    [Fact]
    public void AddSqlProject_WithSkipWhenDeployed()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var targetDatabase = appBuilder.AddSqlServer("sql").AddDatabase("test");
        appBuilder.AddSqlProject<TestProject>("MySqlProject")
            .WithReference(targetDatabase)
            .WithSkipWhenDeployed();

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("MySqlProject", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.HasAnnotationOfType<DacpacSkipWhenDeployedAnnotation>());
    }

    private sealed class RecordingDacpacDeployer : IDacpacDeployer
    {
        public string? DacpacPath { get; private set; }

        public bool DacpacExistedDuringDeploy { get; private set; }

        public void Deploy(string dacpacPath, DacDeployOptions options, string targetConnectionString, string? targetDatabaseName, ILogger deploymentLogger, CancellationToken cancellationToken)
        {
            DacpacPath = dacpacPath;
            DacpacExistedDuringDeploy = File.Exists(dacpacPath);
        }
    }

    private sealed class BlockingDacpacDeployer : IDacpacDeployer
    {
        private readonly ConcurrentQueue<string> _dacpacPaths = new();
        private readonly TaskCompletionSource _twoDeploymentsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _releaseDeployments = new();
        private int _deploymentCount;

        public string[] DacpacPaths => _dacpacPaths.ToArray();

        public Task WaitForTwoDeploymentsAsync(TimeSpan timeout)
            => _twoDeploymentsStarted.Task.WaitAsync(timeout);

        public void ReleaseDeployments() => _releaseDeployments.Set();

        public void Deploy(string dacpacPath, DacDeployOptions options, string targetConnectionString, string? targetDatabaseName, ILogger deploymentLogger, CancellationToken cancellationToken)
        {
            _dacpacPaths.Enqueue(dacpacPath);
            if (Interlocked.Increment(ref _deploymentCount) == 2)
            {
                _twoDeploymentsStarted.TrySetResult();
            }

            _releaseDeployments.Wait(cancellationToken);
        }
    }
}
