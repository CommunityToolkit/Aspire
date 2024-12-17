using Aspire.Hosting;

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
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        var dacpacPath = sqlProjectResource.GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.Equal(Path.Combine(TestPackage.NuGetPackageCache, "erikej.dacpac.chinook", "1.0.0", "tools", "ErikEJ.Dacpac.Chinook.dacpac"), dacpacPath);
        Assert.True(File.Exists(dacpacPath));
    }

    [Fact]
    public void AddSqlPackage_WithExplicitRelativePath()
    {
         // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddSqlPackage<TestPackage>("chinook").WithDacpac("tools/ErikEJ.Dacpac.Chinook2.dacpac");
        
        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        
        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlProjectResource>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        var dacpacPath = sqlProjectResource.GetDacpacPath();
        Assert.NotNull(dacpacPath);
        Assert.Equal(Path.Combine(TestPackage.NuGetPackageCache, "erikej.dacpac.chinook", "1.0.0", "tools", "ErikEJ.Dacpac.Chinook2.dacpac"), dacpacPath);
    }
}