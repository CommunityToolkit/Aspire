using Aspire.Hosting;
using Microsoft.SqlServer.Dac;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class AddSqlPackageTests
{
    [Fact]
    public void AddSqlPackage_WithPackageMetadata()
    {
         // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlPackage<TestPackage>("chinook");
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlPackageResource<TestPackage>>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        var dacpacPath = ((IResourceWithDacpac)sqlProjectResource).GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.Equal(Path.Combine(TestPackage.NuGetPackageCache, "erikej.dacpac.chinook", "1.0.0", "tools", "ErikEJ.Dacpac.Chinook.dacpac"), dacpacPath);
        Assert.True(File.Exists(dacpacPath));
    }

    [Fact]
    public void AddSqlPackage_WithExplicitRelativePath()
    {
         // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlPackage<TestPackage>("chinook").WithDacpac(Path.Combine("tools", "ErikEJ.Dacpac.Chinook2.dacpac"));
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlPackageResource<TestPackage>>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        var dacpacPath = ((IResourceWithDacpac)sqlProjectResource).GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.Equal(Path.Combine(TestPackage.NuGetPackageCache, "erikej.dacpac.chinook", "1.0.0", "tools", "ErikEJ.Dacpac.Chinook2.dacpac"), dacpacPath);
    }

    [Fact]
    public void AddSqlPackage_WithoutDeploymentOptions()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlPackage<TestPackage>("chinook");

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlPackageResource<TestPackage>>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        Assert.False(sqlProjectResource.TryGetLastAnnotation(out ConfigureDacDeployOptionsAnnotation? _));

        var options = ((IResourceWithDacpac)sqlProjectResource).GetDacpacDeployOptions();
        Assert.NotNull(options);
        Assert.Equivalent(new DacDeployOptions(), options);
    }

    [Fact]
    public void AddSqlPackage_WithDeploymentOptions()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        Action<DacDeployOptions> configureAction = options => options.IncludeCompositeObjects = true;

        appBuilder.AddSqlPackage<TestPackage>("chinook").WithConfigureDacDeployOptions(configureAction);

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlPackageResource<TestPackage>>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.TryGetLastAnnotation(out ConfigureDacDeployOptionsAnnotation? configureDacDeployOptionsAnnotation));
        Assert.Same(configureAction, configureDacDeployOptionsAnnotation.ConfigureDeploymentOptions);

        var options = ((IResourceWithDacpac)sqlProjectResource).GetDacpacDeployOptions();
        Assert.True(options.IncludeCompositeObjects);
    }
}