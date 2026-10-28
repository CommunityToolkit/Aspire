using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
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
