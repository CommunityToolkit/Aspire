using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithCertificateTrustTests
{
    [Fact]
    public void WithPerlCertificateTrust_AddsCertificateTrustAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

#pragma warning disable CTASPIREPERL001
        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlCertificateTrust();
#pragma warning restore CTASPIREPERL001

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

#pragma warning disable CTASPIREPERL001
        Assert.Throws<ArgumentNullException>(() => builder.WithPerlCertificateTrust());
#pragma warning restore CTASPIREPERL001
    }

    [Fact]
    public void WithPerlCertificateTrust_CalledTwice_DoesNotDuplicateAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

#pragma warning disable CTASPIREPERL001
        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlCertificateTrust()
            .WithPerlCertificateTrust();
#pragma warning restore CTASPIREPERL001

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // Verify the marker annotation is present and singular
        Assert.Single(resource.Annotations.OfType<CommunityToolkit.Aspire.Hosting.Perl.Annotations.PerlCertificateTrustAnnotation>());

        var annotations = resource.Annotations
            .OfType<CertificateTrustConfigurationCallbackAnnotation>()
            .ToList();
        Assert.Single(annotations);
    }

    [Fact]
    public void WithPerlCertificateTrust_PropagatesEnvVarsToInstallerChildResources()
    {
        var builder = DistributedApplication.CreateBuilder();

#pragma warning disable CTASPIREPERL001
        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithPackage("Mojolicious")
            .WithPerlCertificateTrust();
#pragma warning restore CTASPIREPERL001

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        // Verify the cert trust marker annotation was propagated to the installer
        Assert.Single(installerResource.Annotations.OfType<CommunityToolkit.Aspire.Hosting.Perl.Annotations.PerlCertificateTrustAnnotation>());

        // Verify exactly one env callback was added for cert trust
        Assert.Single(installerResource.Annotations.OfType<EnvironmentCallbackAnnotation>());
    }

    [Fact]
    public void WithPerlCertificateTrust_BeforeWithPackage_StillPropagates()
    {
        var builder = DistributedApplication.CreateBuilder();

#pragma warning disable CTASPIREPERL001
        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlCertificateTrust()
#pragma warning restore CTASPIREPERL001
            .WithCpanMinus()
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        // Verify the cert trust marker annotation was propagated even though
        // WithPerlCertificateTrust() was called before WithPackage()
        Assert.Single(installerResource.Annotations.OfType<CommunityToolkit.Aspire.Hosting.Perl.Annotations.PerlCertificateTrustAnnotation>());
    }
}
