using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

// SSL_CERT_FILE is process-wide, so these tests must not overlap other collections.
[CollectionDefinition(nameof(WithCertificateTrustTests), DisableParallelization = true)]
public class WithCertificateTrustTestsCollection;

[Collection(nameof(WithCertificateTrustTests))]
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
            .WithCpanMinus()
            .WithPackage("Mojolicious")
            .WithProjectDependencies()
            .WithPerlCertificateTrust()
            .WithPerlCertificateTrust();
#pragma warning restore CTASPIREPERL001

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Single(appModel.Resources.OfType<PerlAppResource>());
        Assert.Equal(2, appModel.Resources.OfType<PerlModuleInstallerResource>().Count());

        foreach (var resource in appModel.Resources)
        {
            Assert.Single(resource.Annotations.OfType<PerlCertificateTrustAnnotation>());
            Assert.Single(resource.Annotations.OfType<CertificateTrustConfigurationCallbackAnnotation>());
        }
    }

    [Fact]
    public void Installers_WithoutPerlCertificateTrust_DoNotConfigureCertificateTrust()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithPackage("Mojolicious")
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Equal(2, installers.Count);
        Assert.All(installers, installer =>
        {
            Assert.Empty(installer.Annotations.OfType<PerlCertificateTrustAnnotation>());
            Assert.Empty(installer.Annotations.OfType<CertificateTrustConfigurationCallbackAnnotation>());
        });
    }

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(true, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, true, null)]
    [InlineData(false, false, "shell-ca-bundle.pem")]
    [InlineData(true, false, "shell-ca-bundle.pem")]
    [InlineData(false, true, "shell-ca-bundle.pem")]
    [InlineData(true, true, "shell-ca-bundle.pem")]
    public async Task WithPerlCertificateTrust_InstallersUseTheirCertificateContext(
        bool certificateTrustFirst,
        bool projectDependencies,
        string? processCertificateFile)
    {
        var originalCertificateFile = Environment.GetEnvironmentVariable("SSL_CERT_FILE");
        try
        {
            Environment.SetEnvironmentVariable("SSL_CERT_FILE", processCertificateFile);
            Assert.Equal(processCertificateFile, Environment.GetEnvironmentVariable("SSL_CERT_FILE"));

            var builder = DistributedApplication.CreateBuilder();
            var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
                .WithCpanMinus();

#pragma warning disable CTASPIREPERL001
            if (certificateTrustFirst)
            {
                perlApp.WithPerlCertificateTrust();
            }

            if (projectDependencies)
            {
                perlApp.WithProjectDependencies();
            }
            else
            {
                perlApp.WithPackage("Mojolicious");
            }

            if (!certificateTrustFirst)
            {
                perlApp.WithPerlCertificateTrust();
            }
#pragma warning restore CTASPIREPERL001

            using var app = builder.Build();

            var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
            var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

            foreach (var resource in new IResource[] { perlApp.Resource, installerResource })
            {
                var environment = new Dictionary<string, object>();
                var certificateDirectory = Path.Combine(builder.AppHostDirectory, resource.Name, "certs");
                var bundlePath = ReferenceExpression.Create($"{Path.Combine(certificateDirectory, "bundle.pem")}");
                var certificateContext = new CertificateTrustConfigurationCallbackAnnotationContext
                {
                    ExecutionContext = builder.ExecutionContext,
                    Resource = resource,
                    Arguments = [],
                    EnvironmentVariables = environment,
                    CertificateBundlePath = bundlePath,
                    CertificateDirectoriesPath = ReferenceExpression.Create($"{certificateDirectory}"),
                    Scope = CertificateTrustScope.Override,
                    CancellationToken = TestContext.Current.CancellationToken
                };

                foreach (var callback in resource.Annotations.OfType<CertificateTrustConfigurationCallbackAnnotation>())
                {
                    await callback.Callback(certificateContext);
                }

                var environmentContext = new EnvironmentCallbackContext(builder.ExecutionContext, environment);
                foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                {
                    await callback.Callback(environmentContext);
                }

                Assert.All(new[] { "SSL_CERT_FILE", "PERL_LWP_SSL_CA_FILE", "MOJO_CA_FILE" }, variable =>
                {
                    Assert.True(environment.TryGetValue(variable, out var value), $"{resource.Name} is missing {variable}.");
                    Assert.Same(bundlePath, value);
                });
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("SSL_CERT_FILE", originalCertificateFile);
        }
    }
}
