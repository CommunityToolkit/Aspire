using Aspire.Hosting;
using Microsoft.SqlServer.Dac;

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
}
