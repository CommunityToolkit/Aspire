using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class AddSqlPackageTests
{
    [Theory]
    [InlineData(".NETCoreApp,Version=v10.0", "net10.0")]
    [InlineData(".NETCoreApp,Version=v11.0", "net11.0")]
    public void PackageDownloadProject_TargetsAppHostFramework(string frameworkName, string expectedTargetFramework)
    {
        var package = new SqlPackageRestoreAnnotation("Company.Database", "1.2.3", Path.GetTempPath());

        var project = SqlProjectDacpacResolver.CreatePackageDownloadProject(package, frameworkName);

        Assert.Equal(expectedTargetFramework, project.Root?.Element("PropertyGroup")?.Element("TargetFramework")?.Value);
    }

    [Fact]
    public void PackageDownloadProject_TargetsRunningAppHostFramework()
    {
        var targetAttribute = Assert.Single(typeof(AddSqlPackageTests).Assembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), inherit: false).Cast<TargetFrameworkAttribute>());
        var version = new FrameworkName(targetAttribute.FrameworkName).Version;
        var package = new SqlPackageRestoreAnnotation("Company.Database", "1.2.3", Path.GetTempPath());

        var project = SqlProjectDacpacResolver.CreatePackageDownloadProject(package, AppContext.TargetFrameworkName);

        Assert.Equal($"net{version.Major}.{version.Minor}", project.Root?.Element("PropertyGroup")?.Element("TargetFramework")?.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(".NETFramework,Version=v4.8")]
    public void PackageDownloadProject_RejectsUnavailableOrUnsupportedFramework(string? frameworkName)
    {
        var package = new SqlPackageRestoreAnnotation("Company.Database", "1.2.3", Path.GetTempPath());

        Assert.Throws<InvalidOperationException>(() => SqlProjectDacpacResolver.CreatePackageDownloadProject(package, frameworkName));
    }

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

    [Theory]
    [InlineData("2.3.4-preview.1", true)]
    [InlineData("2.3.4.0-preview.1", false)]
    public async Task AddSqlPackage_FromIdAndVersionRestoresExactPackageBeforeDeploying(string requestedVersion, bool useCustomPath)
    {
        var packageId = CreateUniquePackageId();
        var version = "2.3.4-preview.1";
        var relativeDacpacPath = useCustomPath ? Path.Combine("schema", "custom.dacpac") : Path.Combine("tools", $"{packageId}.dacpac");
        var appHostDirectory = Path.Combine(Path.GetTempPath(), $"sql package test {Guid.NewGuid():N}");
        var feedDirectory = Path.Combine(appHostDirectory, "feed");
        Directory.CreateDirectory(appHostDirectory);
        CreateNuGetPackage(feedDirectory, packageId, version, relativeDacpacPath);
        CreateNuGetConfig(appHostDirectory, feedDirectory, Path.Combine(appHostDirectory, "packages"));

        try
        {
            var deployer = new RecordingDacpacDeployer();
            var appBuilder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { ProjectDirectory = appHostDirectory });
            appBuilder.Configuration["ConnectionStrings:Target"] = "Server=localhost;Database=Target;Integrated Security=True;TrustServerCertificate=True";
            appBuilder.Services.AddSingleton<IDacpacDeployer>(deployer);

            var package = appBuilder.AddSqlPackage("schema", packageId, requestedVersion);
            if (useCustomPath)
            {
                package.WithDacpac(relativeDacpacPath);
            }
            var target = appBuilder.AddConnectionString("Target");
            package.WithReference(target);

            using var app = appBuilder.Build();
            var publishService = app.Services.GetRequiredService<SqlProjectPublishService>();

            await publishService.PublishSqlProject(package.Resource, target.Resource, null, CancellationToken.None);

            Assert.Equal(1, deployer.DeployCount);
            Assert.NotNull(deployer.DacpacPath);
            Assert.True(File.Exists(deployer.DacpacPath));
            Assert.EndsWith(Path.Combine(packageId.ToLowerInvariant(), version.ToLowerInvariant(), relativeDacpacPath), deployer.DacpacPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(package.Resource.TryGetLastAnnotation(out DacpacMetadataAnnotation? resolvedDacpac));
            Assert.Equal(deployer.DacpacPath, resolvedDacpac.DacpacPath);

            await publishService.PublishSqlProject(package.Resource, target.Resource, null, CancellationToken.None);

            Assert.Equal(deployer.DacpacPath, ((IResourceWithDacpac)package.Resource).GetDacpacPath());
            Assert.Equal(2, deployer.DeployCount);
            Assert.True(package.Resource.TryGetLastAnnotation(out SqlPackageRestoreAnnotation? originalPackageInput));
            Assert.Equal(useCustomPath ? relativeDacpacPath : null, originalPackageInput.DacpacRelativePath);
        }
        finally
        {
            Directory.Delete(appHostDirectory, recursive: true);
            DeleteRestoredTestPackage(packageId);
        }
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

    [Theory]
    [InlineData("[1.0.0]")]
    [InlineData("1.*")]
    [InlineData("1.0.0-invalid..label")]
    [InlineData("1.0.0.0.0")]
    public void AddSqlPackage_RejectsInvalidOrRangeVersions(string version)
    {
        var builder = DistributedApplication.CreateBuilder();

        var exception = Assert.Throws<ArgumentException>(() => builder.AddSqlPackage("schema", "Company.Database", version));

        Assert.Equal("version", exception.ParamName);
        Assert.Empty(builder.Resources.OfType<SqlProjectResource>());
    }

    [Theory]
    [InlineData("_Company.Database", true)]
    [InlineData("Company.Database_", true)]
    [InlineData("Company..Database", false)]
    [InlineData("Company.-Database", false)]
    public void AddSqlPackage_ValidatesPackageIdUsingNuGetRules(string packageId, bool isValid)
    {
        var builder = DistributedApplication.CreateBuilder();

        if (isValid)
        {
            builder.AddSqlPackage("schema", packageId, "1.0.0");
            Assert.Single(builder.Resources.OfType<SqlProjectResource>());
        }
        else
        {
            var exception = Assert.Throws<ArgumentException>(() => builder.AddSqlPackage("schema", packageId, "1.0.0"));

            Assert.Equal("packageId", exception.ParamName);
            Assert.Empty(builder.Resources.OfType<SqlProjectResource>());
        }
    }

    [Theory]
    [InlineData("missing-package")]
    [InlineData("missing-dacpac")]
    [InlineData("cancelled")]
    [InlineData("outside-package")]
    public async Task AddSqlPackage_PreparationFailurePreventsDeployment(string failure)
    {
        var appHostDirectory = Path.Combine(Path.GetTempPath(), $"sql-package-error-{Guid.NewGuid():N}");
        var feedDirectory = Path.Combine(appHostDirectory, "feed");
        var packageId = CreateUniquePackageId();
        var version = "1.0.0";
        CreateNuGetPackage(feedDirectory, packageId, version, Path.Combine("tools", $"{packageId}.dacpac"));
        CreateNuGetConfig(appHostDirectory, feedDirectory, Path.Combine(appHostDirectory, "packages"));

        try
        {
            var deployer = new RecordingDacpacDeployer();
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { ProjectDirectory = appHostDirectory });
            builder.Configuration["ConnectionStrings:Target"] = "Server=localhost;Database=Target;Integrated Security=True";
            builder.Services.AddSingleton<IDacpacDeployer>(deployer);
            var package = builder.AddSqlPackage("schema", packageId, failure == "missing-package" ? "9.9.9" : version);
            if (failure == "missing-dacpac")
            {
                package.WithDacpac("missing.dacpac");
            }
            else if (failure == "outside-package")
            {
                package.WithDacpac(Path.Combine("..", "outside.dacpac"));
            }

            var target = builder.AddConnectionString("Target");
            package.WithReference(target);
            using var app = builder.Build();
            using var cancellation = new CancellationTokenSource();
            if (failure == "cancelled")
            {
                cancellation.Cancel();
            }

            await app.Services.GetRequiredService<SqlProjectPublishService>()
                .PublishSqlProject(package.Resource, target.Resource, null, cancellation.Token);

            Assert.Equal(0, deployer.DeployCount);
            Assert.Null(package.Resource.DacpacPath);
            var temporaryRoot = Path.Combine(appHostDirectory, "obj", "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects");
            if (Directory.Exists(temporaryRoot))
            {
                Assert.Empty(Directory.EnumerateDirectories(temporaryRoot));
            }
        }
        finally
        {
            Directory.Delete(appHostDirectory, recursive: true);
            DeleteRestoredTestPackage(packageId);
        }
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

    [Fact]
    public void AddSqlPackage_WithDeploymentOptions_FromFile()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        var optionsPath = "Database.publish.xml";

        appBuilder.AddSqlPackage<TestPackage>("chinook").WithDacDeployOptions(optionsPath);

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlPackageResource<TestPackage>>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.TryGetLastAnnotation(out DacDeployOptionsAnnotation? dacDeployOptionsAnnotation));
        Assert.Equal(optionsPath, dacDeployOptionsAnnotation.OptionsPath);

        var options = ((IResourceWithDacpac)sqlProjectResource).GetDacpacDeployOptions();
        Assert.False(options.BlockOnPossibleDataLoss);
    }

    [Fact]
    public void AddSqlPackage_WithExplicitStart()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var targetDatabase = appBuilder.AddSqlServer("sql").AddDatabase("test");
        appBuilder.AddSqlPackage<TestPackage>("chinook")
            .WithReference(targetDatabase)
            .WithExplicitStart();

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlPackageResource<TestPackage>>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.HasAnnotationOfType<ExplicitStartupAnnotation>());
    }

    [Fact]
    public void AddSqlPackage_WithSkipWhenDeployed()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var targetDatabase = appBuilder.AddSqlServer("sql").AddDatabase("test");
        appBuilder.AddSqlPackage<TestPackage>("chinook")
            .WithReference(targetDatabase)
            .WithSkipWhenDeployed();

        // Act
        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Assert
        var sqlProjectResource = Assert.Single(appModel.Resources.OfType<SqlPackageResource<TestPackage>>());
        Assert.Equal("chinook", sqlProjectResource.Name);

        Assert.True(sqlProjectResource.HasAnnotationOfType<DacpacSkipWhenDeployedAnnotation>());
    }

    internal static void CreateNuGetPackage(string feedDirectory, string packageId, string version, string dacpacRelativePath, string? dacpacSourcePath = null)
    {
        Directory.CreateDirectory(feedDirectory);
        var packagePath = Path.Combine(feedDirectory, $"{packageId}.{version}.nupkg");

        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        var nuspecNamespace = XNamespace.Get("http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd");
        var nuspecEntry = archive.CreateEntry($"{packageId}.nuspec");
        using (var writer = new StreamWriter(nuspecEntry.Open()))
        {
            var nuspec = new XDocument(
                new XElement(nuspecNamespace + "package",
                    new XElement(nuspecNamespace + "metadata",
                        new XElement(nuspecNamespace + "id", packageId),
                        new XElement(nuspecNamespace + "version", version),
                        new XElement(nuspecNamespace + "authors", "Test"),
                        new XElement(nuspecNamespace + "description", "Local SQL DACPAC test package"))));
            writer.Write(nuspec.ToString(SaveOptions.DisableFormatting));
        }

        if (dacpacSourcePath is not null)
        {
            archive.CreateEntryFromFile(dacpacSourcePath, dacpacRelativePath.Replace(Path.DirectorySeparatorChar, '/'));
            return;
        }

        var dacpacEntry = archive.CreateEntry(dacpacRelativePath.Replace(Path.DirectorySeparatorChar, '/'));
        using var dacpacStream = dacpacEntry.Open();
        dacpacStream.Write([1, 2, 3]);
    }

    internal static string CreateUniquePackageId() => $"Company.Database.T{Guid.NewGuid():N}";

    internal static void DeleteRestoredTestPackage(string packageId)
    {
        // NuGet stores packages here instead of the test's temporary globalPackagesFolder when this is set.
        var globalPackagesFolder = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrEmpty(globalPackagesFolder))
        {
            return;
        }

        var packageDirectory = Path.Combine(globalPackagesFolder, packageId.ToLowerInvariant());
        if (Directory.Exists(packageDirectory))
        {
            Directory.Delete(packageDirectory, recursive: true);
        }
    }

    internal static void CreateNuGetConfig(string appHostDirectory, string feedDirectory, string packagesDirectory)
    {
        var configuration = new XDocument(
            new XElement("configuration",
                new XElement("packageSources",
                    new XElement("clear"),
                    new XElement("add", new XAttribute("key", "local"), new XAttribute("value", feedDirectory))),
                new XElement("config",
                    new XElement("add", new XAttribute("key", "globalPackagesFolder"), new XAttribute("value", packagesDirectory)))));
        configuration.Save(Path.Combine(appHostDirectory, "NuGet.Config"));
    }

    private sealed class RecordingDacpacDeployer : IDacpacDeployer
    {
        public string? DacpacPath { get; private set; }

        public int DeployCount { get; private set; }

        public void Deploy(string dacpacPath, DacDeployOptions options, string targetConnectionString, string? targetDatabaseName, ILogger deploymentLogger, CancellationToken cancellationToken)
        {
            DeployCount++;
            DacpacPath = dacpacPath;
        }
    }
}