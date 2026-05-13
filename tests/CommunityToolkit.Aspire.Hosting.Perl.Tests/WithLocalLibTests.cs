using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithLocalLibTests
{
    [Fact]
    public void WithLocalLib_SetsDefaultPath()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithLocalLib();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var localLibResources = resource.Annotations.OfType<PerlLocalLibAnnotation>().ToList();
        Assert.Single(localLibResources);
        Assert.Equal("local", localLibResources[0].Path);
    }

    [Fact]
    public void WithLocalLib_ShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithLocalLib());
    }

    [Fact]
    public void WithLocalLib_ShouldThrowWhenPathIsNullOrEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        Assert.Throws<ArgumentException>(() => resource.WithLocalLib(""));
    }

    [Fact]
    public void WithLocalLib_CustomPath()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithLocalLib("vendor");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var envCallbacks = resource.Annotations.OfType<PerlLocalLibResourceEnvironmentAnnotation>().ToList();
        Assert.Single(envCallbacks);
    }

    [Fact]
    public async Task WithLocalLib_CalledTwice_DoesNotDuplicateResourceEnvironmentCallback()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithLocalLib("local")
            .WithLocalLib("vendor");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var envCallbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.Single(resource.Annotations.OfType<PerlLocalLibResourceEnvironmentAnnotation>());

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var callback in envCallbacks)
        {
            await callback.Callback(context);
        }

        Assert.Contains("vendor", envVars["PERL_LOCAL_LIB_ROOT"].ToString());
    }

    [Theory]
    [InlineData("specificLocal", "packageName", "packageName-installer")]
    public void WithPackageAndLocalLib_InstallerAndPackageAndLocalLibCorrectlyConfigured(
        string expectedLocalLibPath, 
        string packageName, 
        string installerName)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithPackage(packageName)
            .WithLocalLib(expectedLocalLibPath);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlResource = Assert.Single(builder.Resources.OfType<PerlAppResource>());
        var packageManager = Assert.Single(perlResource.Annotations.OfType<PerlPackageManagerAnnotation>());
        var perlRequiredModule = Assert.Single(perlResource.Annotations.OfType<PerlRequiredModuleAnnotation>());
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        var perlLocalLib = Assert.Single(perlResource.Annotations.OfType<PerlLocalLibAnnotation>());

        Assert.Equal(packageName, perlRequiredModule.Name);
        Assert.Equal(PerlPackageManager.Cpanm, packageManager.PackageManager);
        Assert.Equal(installerName, installerResource.Name);        
        Assert.Equal(expectedLocalLibPath, perlLocalLib.Path);
    }
}
