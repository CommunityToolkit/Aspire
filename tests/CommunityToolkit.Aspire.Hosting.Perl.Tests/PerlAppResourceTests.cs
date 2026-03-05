using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlAppResourceTests
{
    #region PerlAppResource Type and Shape

    [Fact]
    public void PerlAppResourceInheritsFromExecutableResource()
    {
        var resource = new PerlAppResource("test", "perl", "scripts");

        Assert.IsAssignableFrom<ExecutableResource>(resource);
    }

    [Fact]
    public void PerlAppResourceImplementsIResourceWithServiceDiscovery()
    {
        var resource = new PerlAppResource("test", "perl", "scripts");

        Assert.IsAssignableFrom<IResourceWithServiceDiscovery>(resource);
    }

    [Fact]
    public void PerlAppResourceSetsNameCorrectly()
    {
        var resource = new PerlAppResource("my-perl-app", "perl", "scripts");

        Assert.Equal("my-perl-app", resource.Name);
    }

    [Fact]
    public void PerlAppResourceSetsCommandCorrectly()
    {
        var resource = new PerlAppResource("test", "perl", "scripts");

        Assert.Equal("perl", resource.Command);
    }

    [Fact]
    public void PerlAppResourceSetsWorkingDirectoryCorrectly()
    {
        var resource = new PerlAppResource("test", "perl", "/path/to/scripts");

        Assert.Equal("/path/to/scripts", resource.WorkingDirectory);
    }

    #endregion

    #region PerlModuleInstallerResource Type and Shape

    [Fact]
    public void PerlModuleInstallerResourceInheritsFromExecutableResource()
    {
        var resource = new PerlModuleInstallerResource("installer", "cpanm", "scripts");

        Assert.IsAssignableFrom<ExecutableResource>(resource);
    }

    [Fact]
    public void PerlModuleInstallerResourceSetsNameCorrectly()
    {
        var resource = new PerlModuleInstallerResource("Mojolicious-installer", "cpanm", "scripts");

        Assert.Equal("Mojolicious-installer", resource.Name);
    }

    #endregion

    #region EntrypointType Enum Coverage

    [Fact]
    public void EntrypointTypeHasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(EntrypointType), EntrypointType.Script));
        Assert.True(Enum.IsDefined(typeof(EntrypointType), EntrypointType.API));
        Assert.True(Enum.IsDefined(typeof(EntrypointType), EntrypointType.Module));
        Assert.True(Enum.IsDefined(typeof(EntrypointType), EntrypointType.Executable));
    }

    #endregion

    #region Application Model Composition

    [Fact]
    public void MultipleResourcesCanBeAddedToModel()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("script-app", "scripts", "worker.pl");
        builder.AddPerlApi("api-app", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resources = appModel.Resources.OfType<PerlAppResource>().ToList();

        Assert.Equal(2, resources.Count);
        Assert.Contains(resources, r => r.Name == "script-app");
        Assert.Contains(resources, r => r.Name == "api-app");
    }

    #endregion
}
