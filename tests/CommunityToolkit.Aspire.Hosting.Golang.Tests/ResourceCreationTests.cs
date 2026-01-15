using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Golang.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void DefaultGolangApp()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<GolangAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("go", resource.Command);
    }

    [Fact]
    public async Task GolangAppWithBuildTagsAsync()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api", buildTags: ["dev"]);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<GolangAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        var args = await resource.GetArgumentListAsync();
        Assert.Collection(
            args,
            arg =>
            {
                Assert.Equal("run", arg);
            },
            arg =>
            {
                Assert.Equal("-tags", arg);
            },
            arg =>
            {
                Assert.Equal("dev", arg);
            },
            arg =>
            {
                Assert.Equal(".", arg);
            }
        );
    }


    [Fact]
    public async Task GolangAppWithExecutableAsync()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api", "./cmd/server");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<GolangAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        var args = await resource.GetArgumentListAsync();
        Assert.Collection(
            args,
            arg =>
            {
                Assert.Equal("run", arg);
            },
            arg =>
            {
                Assert.Equal("./cmd/server", arg);
            }
        );
    }

    [Fact]
    public void GolangAppWithGoModTidyCreatesInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api").WithGoModTidy();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<GolangAppExecutableResource>());
        var installerResource = Assert.Single(appModel.Resources.OfType<GoModInstallerResource>());

        Assert.Equal("golang-go-mod-tidy", installerResource.Name);
        Assert.Equal("go", installerResource.Command);
    }

    [Fact]
    public async Task GolangAppWithGoModTidyHasCorrectArgsAsync()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api").WithGoModTidy();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var installerResource = Assert.Single(appModel.Resources.OfType<GoModInstallerResource>());

        var args = await installerResource.GetArgumentsAsync();
        Assert.Collection(
            args,
            arg => Assert.Equal("mod", arg),
            arg => Assert.Equal("tidy", arg)
        );
    }

    [Fact]
    public void GolangAppWithGoModDownloadCreatesInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api").WithGoModDownload();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Single(appModel.Resources.OfType<GolangAppExecutableResource>());
        var installerResource = Assert.Single(appModel.Resources.OfType<GoModInstallerResource>());

        Assert.Equal("golang-go-mod-download", installerResource.Name);
        Assert.Equal("go", installerResource.Command);
    }

    [Fact]
    public async Task GolangAppWithGoModDownloadHasCorrectArgsAsync()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api").WithGoModDownload();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var installerResource = Assert.Single(appModel.Resources.OfType<GoModInstallerResource>());

        var args = await installerResource.GetArgumentValuesAsync();
        Assert.Collection(
            args,
            arg => Assert.Equal("mod", arg),
            arg => Assert.Equal("download", arg)
        );
    }

    [Fact]
    public void WithGoModTidyNullBuilderThrows()
    {
        IResourceBuilder<GolangAppExecutableResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithGoModTidy());
    }

    [Fact]
    public void WithGoModDownloadNullBuilderThrows()
    {
        IResourceBuilder<GolangAppExecutableResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithGoModDownload());
    }

    [Fact]
    public void GolangAppWithGoModTidyInstallFalseCreatesInstallerWithExplicitStart()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api").WithGoModTidy(install: false);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var golangResource = Assert.Single(appModel.Resources.OfType<GolangAppExecutableResource>());
        var installerResource = Assert.Single(appModel.Resources.OfType<GoModInstallerResource>());

        // Installer should be created even with install: false
        Assert.Equal("golang-go-mod-tidy", installerResource.Name);
        Assert.Equal("go", installerResource.Command);

        // Verify that the installer has ExplicitStartupAnnotation
        Assert.True(installerResource.HasAnnotationOfType<ExplicitStartupAnnotation>());
    }

    [Fact]
    public void GolangAppWithGoModDownloadInstallFalseCreatesInstallerWithExplicitStart()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api").WithGoModDownload(install: false);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var golangResource = Assert.Single(appModel.Resources.OfType<GolangAppExecutableResource>());
        var installerResource = Assert.Single(appModel.Resources.OfType<GoModInstallerResource>());

        // Installer should be created even with install: false
        Assert.Equal("golang-go-mod-download", installerResource.Name);
        Assert.Equal("go", installerResource.Command);

        // Verify that the installer has ExplicitStartupAnnotation
        Assert.True(installerResource.HasAnnotationOfType<ExplicitStartupAnnotation>());
    }
}