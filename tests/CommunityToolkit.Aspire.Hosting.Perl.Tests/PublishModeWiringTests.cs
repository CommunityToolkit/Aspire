using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
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
