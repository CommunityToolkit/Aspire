using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class InstallerIntegrationTests
{
    [Fact, RequiresLinux]
    public void WithPerlbrewEnvironmentAndCpanm_InstallerGetsPerlbrewAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew")
            .WithCpanMinus()
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // The parent resource should have the perlbrew annotation
        Assert.True(resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var annotation));
        Assert.NotNull(annotation.Environment);
    }

    [Fact, RequiresLinux]
    public void WithPerlbrewEnvironmentAndCpanm_DoesNotAddCpanmBootstrapInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/tmp/ctaspire-missing-perlbrew-root")
            .WithCpanMinus()
            .WithPackage("OpenTelemetry::SDK");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.DoesNotContain(
            appModel.Resources.OfType<ExecutableResource>(),
            resource => resource.Name == "perl-app-perlbrew-cpanm-installer");
    }

    [Fact, RequiresLinux]
    public void WithPerlbrewEnvironmentAndCpanm_ModuleInstallerDoesNotWaitForBootstrapInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/tmp/ctaspire-missing-perlbrew-root")
            .WithCpanMinus()
            .WithPackage("OpenTelemetry::SDK");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var moduleInstaller = appModel.Resources
            .OfType<PerlModuleInstallerResource>()
            .Single(r => r.Name.Contains("OpenTelemetry", StringComparison.Ordinal));

        var appResource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());
        PerlAppResourceBuilderExtensions.SetupDependencies(builder, appResource);

        Assert.DoesNotContain(
            moduleInstaller.Annotations.OfType<WaitAnnotation>(),
            wait => wait.Resource.Name == "perl-app-perlbrew-cpanm-installer" && wait.WaitType == WaitType.WaitForCompletion);
    }
}
