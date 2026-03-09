using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CTASPIREPERL001
#pragma warning disable ASPIRECOMMAND001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PackageManagementTests
{
    #region Default Package Manager

    [Fact]
    public void DefaultPackageManagerIsCpan()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpan", annotation.ExecutableName);
    }

    #endregion

    #region WithCpanMinus

    [Fact]
    public void WithCpanMinusChangesPackageManagerToCpanm()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpanm", annotation.ExecutableName);
    }

    [Fact]
    public void WithCpanMinusReplacesDefaultAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // Should have exactly one PerlPackageManagerAnnotation, not two
        var annotations = resource.Annotations.OfType<PerlPackageManagerAnnotation>().ToList();
        Assert.Single(annotations);
        Assert.Equal("cpanm", annotations[0].ExecutableName);
    }

    [Fact]
    public void WithCpanMinusShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithCpanMinus());
    }

    #endregion

    #region WithPackage

    [Fact]
    public void WithPackageCreatesInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Contains("Mojolicious", installerResource.Name);
    }

    [Fact]
    public void WithPackage_ModuleNameWithColons_SanitizesInstallerResourceName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("OpenTelemetry::SDK");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Equal("OpenTelemetry88SDK-installer", installerResource.Name);
    }

    [Fact]
    public void WithPackageAddsRequiredModuleAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("DBI");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotations = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().ToList();
        Assert.Single(annotations);
        Assert.Equal("DBI", annotations[0].Name);
        Assert.False(annotations[0].Force);
        Assert.False(annotations[0].SkipTest);
    }

    [Fact]
    public void WithPackageWithForceAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("DBI", force: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().Single();
        Assert.True(annotation.Force);
    }

    [Fact]
    public void WithPackageWithSkipTestAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("DBI", skipTest: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().Single();
        Assert.True(annotation.SkipTest);
    }

    [Fact]
    public void WithPackageMultiplePackagesCreateMultipleInstallers()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("Mojolicious")
            .WithPackage("DBI")
            .WithPackage("JSON::XS");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Equal(3, installers.Count);
    }

    [Fact]
    public void WithPackageShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithPackage("Mojolicious"));
    }

    [Fact]
    public void WithPackageShouldThrowWhenPackageNameIsNullOrEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        Assert.Throws<ArgumentNullException>(() => resource.WithPackage(null!));
        Assert.Throws<ArgumentException>(() => resource.WithPackage(""));
    }

    #endregion

    #region WithCpanMinus + WithPackage Integration

    [Fact]
    public void WithCpanMinusThenWithPackageUsesCpanm()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpanm", annotation.ExecutableName);

        // Installer should exist
        Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
    }

    [Fact]
    public void WithPackageDefaultUsesCpan()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpan", annotation.ExecutableName);
    }

    #endregion

    #region BuildInstallArgs

    [Fact]
    public void BuildInstallArgs_CpanmBasic()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpanm, "Mojolicious", force: false, skipTest: false);

        Assert.Equal(["Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithForce()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpanm, "Mojolicious", force: true, skipTest: false);

        Assert.Equal(["--force", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpanm, "Mojolicious", force: false, skipTest: true);

        Assert.Equal(["--notest", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithForceAndNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpanm, "Mojolicious", force: true, skipTest: true);

        Assert.Equal(["--force", "--notest", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanBasic()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpan, "DBI", force: false, skipTest: false);

        Assert.Equal(["DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanWithForce()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpan, "DBI", force: true, skipTest: false);

        // cpan requires -i when -f is used
        Assert.Equal(["-f", "-i", "DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanWithNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpan, "DBI", force: false, skipTest: true);

        Assert.Equal(["-T", "DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanWithForceAndNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpan, "DBI", force: true, skipTest: true);

        // cpan requires -i when -f is used
        Assert.Equal(["-f", "-T", "-i", "DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_UndefinedEnumValueThrows()
    {
        Assert.Throws<NotSupportedException>(() =>
            PerlAppResourceBuilderExtensions.BuildInstallArgs((PerlPackageManager)99, "SomeModule", force: false, skipTest: false));
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithLocalLib()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpanm, "Mojolicious", force: false, skipTest: false, localLibPath: "/app/local");

        Assert.Equal(["--local-lib", "/app/local", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithLocalLibAndForceAndNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpanm, "Mojolicious", force: true, skipTest: true, localLibPath: "/app/local");

        Assert.Equal(["--local-lib", "/app/local", "--force", "--notest", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanWithLocalLib_DoesNotAddFlag()
    {
        // cpan does not support --local-lib; it relies on env vars instead
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpan, "DBI", force: false, skipTest: false, localLibPath: "/app/local");

        Assert.Equal(["DBI"], args);
    }

    #endregion

    #region AddPerlApi with Packages

    [Fact]
    public void AddPerlApiAlsoHasDefaultCpanPackageManager()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpan", annotation.ExecutableName);
    }

    [Fact]
    public void AddPerlApiWithCpanMinusAndPackage()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl")
            .WithCpanMinus()
            .WithPackage("Mojolicious", skipTest: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpanm", annotation.ExecutableName);

        var moduleAnnotation = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().Single();
        Assert.Equal("Mojolicious", moduleAnnotation.Name);
        Assert.True(moduleAnnotation.SkipTest);
    }

    #endregion

    #region WithProjectDependencies

    [Fact]
    public void WithProjectDependencies_CreatesProjectInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Equal("perl-app-deps-installer", installerResource.Name);
    }

    [Fact]
    public void WithProjectDependencies_AutoSwitchesToCpanm_WhenDefaultCpan()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal(PerlPackageManager.Cpanm, annotation.PackageManager);
    }

    [Fact]
    public void WithProjectDependencies_KeepsCpanm_WhenAlreadyCpanm()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal(PerlPackageManager.Cpanm, annotation.PackageManager);
    }

    [Fact]
    public void WithProjectDependencies_AddsProjectInstallerAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlProjectInstallerAnnotation>(out var annotation));
        Assert.NotNull(annotation.Resource);
    }

    [Fact]
    public void WithProjectDependencies_IsIdempotent()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Single(installers);
    }

    [Fact]
    public void WithProjectDependencies_ShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithProjectDependencies());
    }

    [Fact]
    public void WithProjectDependencies_CombinesWithPerPackageInstallers()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithPackage("Extra::Module");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Equal(2, installers.Count);
    }

    [Fact]
    public void WithCartonAndLocalLib_ProjectInstallerGetsLocalLibEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCarton()
            .WithProjectDependencies()
            .WithLocalLib("vendor");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        var envCallbacks = installerResource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envCallbacks);
    }

    [Fact]
    public void WithLocalLibWithoutCarton_ProjectInstallerStillGetsLocalLibEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithLocalLib("vendor");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        var envCallbacks = installerResource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envCallbacks);
    }

    #endregion

    #region BuildProjectInstallArgs

    [Fact]
    public void BuildProjectInstallArgs_Cpanm_ReturnsInstalldeps()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Cpanm, cartonDeployment: false);

        Assert.Equal(["--installdeps", "--notest", "."], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_Cpanm_IgnoresDeploymentFlag()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Cpanm, cartonDeployment: true);

        // Deployment flag is only for Carton; cpanm ignores it
        Assert.Equal(["--installdeps", "--notest", "."], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_Carton_ReturnsInstall()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Carton, cartonDeployment: false);

        Assert.Equal(["install"], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_Carton_WithDeployment()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Carton, cartonDeployment: true);

        Assert.Equal(["install", "--deployment"], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_Cpan_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Cpan, cartonDeployment: false));
    }

    [Fact]
    public void BuildProjectInstallArgs_CpanmWithLocalLib()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(
            PerlPackageManager.Cpanm, cartonDeployment: false, localLibPath: "/app/local");

        Assert.Equal(["--local-lib", "/app/local", "--installdeps", "--notest", "."], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_CartonWithLocalLib_DoesNotAddFlag()
    {
        // Carton manages its own local directory; --local-lib is not used
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(
            PerlPackageManager.Carton, cartonDeployment: false, localLibPath: "/app/local");

        Assert.Equal(["install"], args);
    }

    #endregion

    #region WithCarton

    [Fact]
    public void WithCartonChangesPackageManagerToCarton()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCarton();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal(PerlPackageManager.Carton, annotation.PackageManager);
        Assert.Equal("carton", annotation.ExecutableName);
    }

    [Fact]
    public void WithCartonReplacesDefaultAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCarton();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotations = resource.Annotations.OfType<PerlPackageManagerAnnotation>().ToList();
        Assert.Single(annotations);
        Assert.Equal("carton", annotations[0].ExecutableName);
    }

    [Fact]
    public void WithCartonReplacesCpanmAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithCarton();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotations = resource.Annotations.OfType<PerlPackageManagerAnnotation>().ToList();
        Assert.Single(annotations);
        Assert.Equal(PerlPackageManager.Carton, annotations[0].PackageManager);
    }

    [Fact]
    public void WithCartonShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithCarton());
    }

    [Fact]
    public void BuildInstallArgs_CartonThrowsForIndividualPackages()
    {
        Assert.Throws<NotSupportedException>(() =>
            PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Carton, "Mojolicious", force: false, skipTest: false));
    }

    [Fact]
    public void WithCartonThenWithProjectDependencies_UsesCarton()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCarton()
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // Carton should NOT be auto-switched to cpanm
        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal(PerlPackageManager.Carton, annotation.PackageManager);

        // Project installer should still be created
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Single(installers);
    }

    [Fact]
    public void WithCartonThenWithPackage_ThrowsAtConfigurationTime()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ex = Assert.Throws<NotSupportedException>(() =>
            builder.AddPerlScript("perl-app", "scripts", "app.pl")
                .WithCarton()
                .WithPackage("Mojolicious"));

        Assert.Contains("WithPackage() and WithCarton() cannot be combined", ex.Message);
        Assert.Contains("WithProjectDependencies()", ex.Message);
    }

    [Fact]
    public void WithPackageThenWithCarton_ThrowsAtConfigurationTime()
    {
        var builder = DistributedApplication.CreateBuilder();

        var ex = Assert.Throws<NotSupportedException>(() =>
            builder.AddPerlScript("perl-app", "scripts", "app.pl")
                .WithPackage("Mojolicious")
                .WithCarton());

        Assert.Contains("WithPackage() and WithCarton() cannot be combined", ex.Message);
        Assert.Contains("WithProjectDependencies()", ex.Message);
    }

    [Fact]
    public void IsCommandAvailable_ReturnsTrueWhenFoundOnPath()
    {
        var fakePath = "/opt/perl/bin" + Path.PathSeparator + "/usr/local/bin";
        Func<string, bool> fileExists = path => path == Path.Combine("/opt/perl/bin", "cpanm");

        var result = PerlAppResourceBuilderExtensions.IsCommandAvailable("cpanm", fakePath, fileExists);

        Assert.True(result);
    }

    [Fact]
    public void IsCommandAvailable_ReturnsFalseWhenNotOnPath()
    {
        var fakePath = "/opt/perl/bin" + Path.PathSeparator + "/usr/local/bin";
        Func<string, bool> fileExists = _ => false;

        var result = PerlAppResourceBuilderExtensions.IsCommandAvailable("cpanm", fakePath, fileExists);

        Assert.False(result);
    }

    [Fact]
    public void IsCommandAvailable_ReturnsTrueWhenAbsolutePathExists()
    {
        Func<string, bool> fileExists = path => path == "/usr/bin/cpanm";

        var result = PerlAppResourceBuilderExtensions.IsCommandAvailable("/usr/bin/cpanm", null, fileExists);

        Assert.True(result);
    }

    [Fact]
    public void TryResolveCommandFromPath_ReturnsFullPathWhenFound()
    {
        var fakePath = "/first/dir" + Path.PathSeparator + "/second/dir";
        Func<string, bool> fileExists = path => path == Path.Combine("/second/dir", "carton");

        var result = PerlAppResourceBuilderExtensions.TryResolveCommandFromPath("carton", fakePath, fileExists);

        Assert.Equal(Path.Combine("/second/dir", "carton"), result);
    }

    [Fact]
    public void TryResolveCommandFromPath_ReturnsNullWhenNotFound()
    {
        var fakePath = "/first/dir" + Path.PathSeparator + "/second/dir";
        Func<string, bool> fileExists = _ => false;

        var result = PerlAppResourceBuilderExtensions.TryResolveCommandFromPath("carton", fakePath, fileExists);

        Assert.Null(result);
    }

    [Fact]
    public void TryResolveCommandFromPath_ReturnsNullWhenPathIsEmpty()
    {
        Func<string, bool> fileExists = _ => true;

        var result = PerlAppResourceBuilderExtensions.TryResolveCommandFromPath("cpanm", null, fileExists);

        Assert.Null(result);
    }

    #endregion

    #region SetupDependencies (GAP-7 — Dependency DAG)

    [Fact]
    public void SetupDependencies_PerPackageInstallerWaitsForProjectInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var resource = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithPackage("Extra::Module");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlApp = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        PerlAppResourceBuilderExtensions.SetupDependencies(builder, perlApp);

        var projectAnnotation = perlApp.Annotations.OfType<PerlProjectInstallerAnnotation>().Single();
        var moduleAnnotation = perlApp.Annotations.OfType<PerlModuleInstallerAnnotation>().Single();

        var waitAnnotations = moduleAnnotation.Resource.Annotations.OfType<WaitAnnotation>()
            .Where(w => w.Resource == projectAnnotation.Resource)
            .ToList();

        Assert.NotEmpty(waitAnnotations);
    }

    [Fact]
    public void SetupDependencies_NoProjectInstaller_DoesNothing()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlApp = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        PerlAppResourceBuilderExtensions.SetupDependencies(builder, perlApp);

        var moduleAnnotation = perlApp.Annotations.OfType<PerlModuleInstallerAnnotation>().Single();
        var waitAnnotations = moduleAnnotation.Resource.Annotations.OfType<WaitAnnotation>().ToList();

        Assert.DoesNotContain(waitAnnotations, w =>
            w.Resource.Name.Contains("deps-installer"));
    }

    [Fact]
    public void SetupDependencies_MultiplePackages_AllWaitForProjectInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithPackage("DBI")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlApp = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        PerlAppResourceBuilderExtensions.SetupDependencies(builder, perlApp);

        var projectAnnotation = perlApp.Annotations.OfType<PerlProjectInstallerAnnotation>().Single();

        foreach (var moduleAnnotation in perlApp.Annotations.OfType<PerlModuleInstallerAnnotation>())
        {
            var hasWaitForProject = moduleAnnotation.Resource.Annotations
                .OfType<WaitAnnotation>()
                .Any(w => w.Resource == projectAnnotation.Resource);

            Assert.True(hasWaitForProject, $"Installer '{moduleAnnotation.Resource.Name}' should wait for project deps installer");
        }
    }

    #endregion

    #region HasDependencyFiles

    [Fact]
    public void HasDependencyFiles_ReturnsTrueWhenCpanfileExists()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            File.WriteAllText(Path.Combine(tempDir.FullName, "cpanfile"), "requires 'Mojolicious';");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.True(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ReturnsTrueWhenMakefilePLExists()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            File.WriteAllText(Path.Combine(tempDir.FullName, "Makefile.PL"), "use ExtUtils::MakeMaker;");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.True(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ReturnsTrueWhenBuildPLExists()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            File.WriteAllText(Path.Combine(tempDir.FullName, "Build.PL"), "use Module::Build;");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.True(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ReturnsFalseWhenNoDepFilesExist()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            File.WriteAllText(Path.Combine(tempDir.FullName, "app.pl"), "print 'hello';");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.False(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ResolvesRelativePath()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        var subDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, "scripts"));
        try
        {
            File.WriteAllText(Path.Combine(subDir.FullName, "cpanfile"), "requires 'DBI';");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles("scripts", tempDir.FullName);

            Assert.True(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ReturnsFalseForEmptyDirectory()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.False(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    #endregion

    #region WithLocalLib

    [Fact]
    public void WithLocalLib_SetsDefaultPath()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithLocalLib();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var envCallbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.True(envCallbacks.Count > 0);
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

        var envCallbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.True(envCallbacks.Count > 0);
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

    [Fact]
    public void WithPackageAndLocalLib_InstallerGetsLocalLibEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithPackage("Mojolicious")
            .WithLocalLib("local");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        var envCallbacks = installerResource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envCallbacks);
    }

    #endregion

    #region WithPerlCertificateTrust

    [Fact]
    public void WithPerlCertificateTrust_AddsCertificateTrustAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlCertificateTrust();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotations = resource.Annotations
            .OfType<CertificateTrustConfigurationCallbackAnnotation>()
            .ToList();
        Assert.Single(annotations);
    }

    [Fact]
    public void WithPerlCertificateTrust_ShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithPerlCertificateTrust());
    }

    #endregion
}
