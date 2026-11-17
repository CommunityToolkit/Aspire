using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PublishModeWiringTests
{
    [Fact]
    public void PublishMode_ResourceExistsInModel()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", "/tmp/aspire-manifest"]);

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Contains(appModel.Resources, r => r.Name == "perl-app");
    }

    [Fact]
    public void PublishMode_CollidingModuleNames_RetainsRequestsWithoutInstallers()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        Assert.True(builder.ExecutionContext.IsPublishMode);

        var resource = builder.AddPerlScript("app", "scripts", "app.pl")
            .WithPackage("Foo88Bar")
            .WithPackage("Foo::Bar");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Empty(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Empty(resource.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Collection(
            resource.Resource.Annotations.OfType<PerlRequiredModuleAnnotation>(),
            annotation => Assert.Equal("Foo88Bar", annotation.Name),
            annotation => Assert.Equal("Foo::Bar", annotation.Name));
#pragma warning disable ASPIRECOMMAND001
        Assert.DoesNotContain(
            resource.Resource.Annotations.OfType<RequiredCommandAnnotation>(),
            annotation => annotation.Command == "cpan");
#pragma warning restore ASPIRECOMMAND001
    }

    [Fact]
    public async Task PublishMode_DoesNotConfigureCertificateTrustOrCreateInstallers()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        Assert.True(builder.ExecutionContext.IsPublishMode);
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithPackage("Mojolicious")
            .WithProjectDependencies();

        using var app = builder.Build();

        var environment = await perlApp.Resource.GetEnvironmentVariablesAsync(
            DistributedApplicationOperation.Publish, app.Services);

        Assert.Equal("otlp", environment["OTEL_TRACES_EXPORTER"]);
        foreach (var variable in new[]
        {
            "SSL_CERT_FILE", "PERL_LWP_SSL_CA_FILE", "MOJO_CA_FILE",
            "OTEL_EXPORTER_OTLP_CERTIFICATE", "OTEL_PERL_EXPORTER_OTLP_CERTIFICATE"
        })
        {
            Assert.DoesNotContain(variable, environment.Keys);
        }

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Empty(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Empty(perlApp.Resource.Annotations.OfType<WaitAnnotation>());
    }

    [Fact]
    public void RunMode_DoesNotAddDockerfileAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var dockerAnnotations = resource.Annotations
            .OfType<DockerfileBuilderCallbackAnnotation>()
            .ToList();
#pragma warning restore ASPIREDOCKERFILEBUILDER001
        Assert.Empty(dockerAnnotations);
    }
}
