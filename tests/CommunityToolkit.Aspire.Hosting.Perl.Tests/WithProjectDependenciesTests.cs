using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithProjectDependenciesTests
{
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

        var annotation = Assert.Single(resource.Annotations.OfType<PerlPackageManagerAnnotation>());
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

        var annotation = Assert.Single(resource.Annotations.OfType<PerlPackageManagerAnnotation>());
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

        var annotation = Assert.Single(resource.Annotations.OfType<PerlProjectInstallerAnnotation>());
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
    public void WithProjectDependencies_CartonDeployment_WarnsMissingSnapshot()
    {
        using var tempDir = new TempDirectory();
        // Create a cpanfile but NO cpanfile.snapshot
        File.WriteAllText(Path.Combine(tempDir.Path, "cpanfile"), "requires 'Mojolicious';");

        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", tempDir.Path, "app.pl")
            .WithCarton()
            .WithProjectDependencies(cartonDeployment: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // The deployment annotation should be stored
        var annotation = resource.Annotations.OfType<CommunityToolkit.Aspire.Hosting.Perl.Annotations.PerlCartonDeploymentAnnotation>().Last();
        Assert.True(annotation.UseDeployment);

        // A RequiredCommandAnnotation for cpanfile.snapshot should be added since the snapshot is missing
#pragma warning disable ASPIRECOMMAND001
        var snapshotCheck = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .SingleOrDefault(a => a.Command == "cpanfile.snapshot");
#pragma warning restore ASPIRECOMMAND001
        Assert.NotNull(snapshotCheck);
    }

    [Fact]
    public void WithProjectDependencies_CartonDeployment_NoWarningWhenSnapshotExists()
    {
        using var tempDir = new TempDirectory();
        // Create both cpanfile AND cpanfile.snapshot
        File.WriteAllText(Path.Combine(tempDir.Path, "cpanfile"), "requires 'Mojolicious';");
        File.WriteAllText(Path.Combine(tempDir.Path, "cpanfile.snapshot"), "DISTRIBUTIONS\n");

        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", tempDir.Path, "app.pl")
            .WithCarton()
            .WithProjectDependencies(cartonDeployment: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // No cpanfile.snapshot RequiredCommandAnnotation should be present since the file exists
#pragma warning disable ASPIRECOMMAND001
        var snapshotCheck = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .SingleOrDefault(a => a.Command == "cpanfile.snapshot");
#pragma warning restore ASPIRECOMMAND001
        Assert.Null(snapshotCheck);
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

        Assert.Single(installerResource.Annotations.OfType<EnvironmentCallbackAnnotation>());
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

        Assert.Single(installerResource.Annotations.OfType<EnvironmentCallbackAnnotation>());
    }
}
