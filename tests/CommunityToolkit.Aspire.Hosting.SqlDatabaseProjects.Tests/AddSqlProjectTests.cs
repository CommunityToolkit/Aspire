using Aspire.Hosting;

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

        var dacpacPath = sqlProjectResource.GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.True(File.Exists(dacpacPath));
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
        Assert.Equal(Path.Combine(appBuilder.AppHostDirectory, TestProject.RelativePath), dacpacMetadataAnnotation.DacpacPath);

        var dacpacPath = sqlProjectResource.GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.True(File.Exists(dacpacPath));
    }

    [Fact]
    public void PublishTo_AddsRequiredServices()
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
    }
}
