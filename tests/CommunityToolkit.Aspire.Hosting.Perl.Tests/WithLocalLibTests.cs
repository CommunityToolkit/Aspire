using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using CommunityToolkit.Aspire.Testing;
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
    [InlineData(false, null)]
    [InlineData(false, "vendor/perl5")]
    [InlineData(true, "vendor/perl5")]
    public async Task WithCartonLocalLib_ProjectInstallerAndAppUseSameInstallRoot(bool localLibFirst, string? localLibPath)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "cpanfile"), "# dependencies\n");
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", temp.Path, "app.pl");

        if (localLibFirst && localLibPath is not null)
        {
            resource.WithLocalLib(localLibPath);
        }

        resource.WithCarton().WithProjectDependencies(cartonDeployment: false);

        if (!localLibFirst && localLibPath is not null)
        {
            resource.WithLocalLib(localLibPath);
        }

        using var app = builder.Build();
        var installer = Assert.Single(builder.Resources.OfType<PerlModuleInstallerResource>());
        var appEnvironment = await resource.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);
        var installerEnvironment = await installer.GetEnvironmentVariablesAsync(serviceProvider: app.Services);
        var expectedRoot = localLibPath is null
            ? Path.Combine(temp.Path, "local")
            : Path.Combine(temp.Path, "vendor", "perl5");
        var expectedPerl5Lib = Path.Combine(expectedRoot, "lib", "perl5");

        Assert.Equal(expectedPerl5Lib, appEnvironment["PERL5LIB"]);
        Assert.Equal(expectedRoot, appEnvironment["PERL_LOCAL_LIB_ROOT"]);
        Assert.Equal(expectedPerl5Lib, installerEnvironment["PERL5LIB"]);
        Assert.Equal(expectedRoot, installerEnvironment["PERL_LOCAL_LIB_ROOT"]);
        Assert.True(installerEnvironment.TryGetValue("PERL_CARTON_PATH", out var installerRoot),
            "Carton defaults to <cpanfile directory>/local unless its install path is configured.");
        Assert.Equal(expectedRoot, installerRoot);
        Assert.True(appEnvironment.TryGetValue("PERL_CARTON_PATH", out var appRoot),
            "Later Carton commands must use the same local::lib root as the installer.");
        Assert.Equal(expectedRoot, appRoot);
        Assert.DoesNotContain("PERL_MM_OPT", appEnvironment.Keys);
        Assert.DoesNotContain("PERL_MB_OPT", appEnvironment.Keys);
        Assert.DoesNotContain("PERL_MM_OPT", installerEnvironment.Keys);
        Assert.DoesNotContain("PERL_MB_OPT", installerEnvironment.Keys);
    }

    [Fact]
    public async Task WithCartonLocalLib_ReconfiguredPath_InstallerUsesLatestRoot()
    {
        using var temp = new TempDirectory();
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", temp.Path, "app.pl")
            .WithCarton()
            .WithProjectDependencies()
            .WithLocalLib("first")
            .WithLocalLib("vendor/perl5");

        using var app = builder.Build();
        var installer = Assert.Single(builder.Resources.OfType<PerlModuleInstallerResource>());
        var environment = await installer.GetEnvironmentVariablesAsync(serviceProvider: app.Services);
        var expectedRoot = Path.Combine(temp.Path, "vendor", "perl5");

        Assert.Equal(expectedRoot, environment["PERL_CARTON_PATH"]);
        Assert.Equal(expectedRoot, environment["PERL_LOCAL_LIB_ROOT"]);
        Assert.Equal(Path.Combine(expectedRoot, "lib", "perl5"), environment["PERL5LIB"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("vendor/perl5")]
    public async Task WithCartonLocalLib_SwitchedToCpanm_UsesFinalPackageManagerEnvironment(string? localLibPath)
    {
        using var temp = new TempDirectory();
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", temp.Path, "app.pl").WithCarton();
        if (localLibPath is not null)
        {
            resource.WithLocalLib(localLibPath);
        }

        resource.WithCpanMinus();

        using var app = builder.Build();
        var environment = await resource.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.DoesNotContain("PERL_CARTON_PATH", environment.Keys);
        if (localLibPath is null)
        {
            Assert.DoesNotContain("PERL5LIB", environment.Keys);
            Assert.DoesNotContain("PERL_LOCAL_LIB_ROOT", environment.Keys);
        }
        else
        {
            var expectedRoot = Path.Combine(temp.Path, "vendor", "perl5");
            Assert.Equal(Path.Combine(expectedRoot, "lib", "perl5"), environment["PERL5LIB"]);
            Assert.Equal($"INSTALL_BASE={expectedRoot}", environment["PERL_MM_OPT"]);
            Assert.Equal($"--install_base {expectedRoot}", environment["PERL_MB_OPT"]);
        }
    }

    [Theory]
    [InlineData("specificLocal", "packageName", "perl-app-packageName-installer")]
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
