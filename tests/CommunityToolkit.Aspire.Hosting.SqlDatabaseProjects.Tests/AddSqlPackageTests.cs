using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class AddSqlPackageTests
{
    [Fact]
    public void AddSqlPackage_WithPackageMetadata()
    {
         // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlPackage<TestPackage>("master");
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("master", sqlProjectResource.Name);

        var dacpacPath = sqlProjectResource.GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.Equal(Path.Combine(TestPackage.PackageBasePath, "tools", "Microsoft.SqlServer.Dacpacs.Master.dacpac"), dacpacPath);
    }

    [Fact]
    public void AddSqlPackage_WithExplicitRelativePath()
    {
         // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlPackage<TestPackage>("master").WithDacpac("tools/master.dacpac");
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("master", sqlProjectResource.Name);

        var dacpacPath = sqlProjectResource.GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.True(File.Exists(dacpacPath));
    }
}