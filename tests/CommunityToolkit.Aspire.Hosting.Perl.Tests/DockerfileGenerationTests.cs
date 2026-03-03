using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIREDOCKERFILEBUILDER001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class DockerfileGenerationTests
{
    #region BuildCpanmDockerfile

    [Fact]
    public void BuildCpanmDockerfile_UsesSingleStage()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(builder, "app.pl", "perl:5-slim");

        Assert.Single(builder.Stages);
    }

    [Fact]
    public void BuildCpanmDockerfile_StageHasStatements()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(builder, "app.pl", "perl:5-slim");

        var stage = builder.Stages[0];
        Assert.NotEmpty(stage.Statements);
    }

    #endregion

    #region BuildCartonDockerfile

    [Fact]
    public void BuildCartonDockerfile_ProducesMultiStage()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(builder, "app.pl", "perl:5-slim", "perl:5");

        Assert.Equal(2, builder.Stages.Count);
    }

    [Fact]
    public void BuildCartonDockerfile_BuildStageNamedBuild()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(builder, "app.pl", "perl:5-slim", "perl:5");

        Assert.Equal("build", builder.Stages[0].StageName);
    }

    [Fact]
    public void BuildCartonDockerfile_RuntimeStageHasStatements()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(builder, "app.pl", "perl:5-slim", "perl:5");

        Assert.NotEmpty(builder.Stages[1].Statements);
    }

    #endregion

    #region PublishMode Wiring

    [Fact]
    public void PublishMode_ResourceExistsInModel()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", "/tmp/aspire-manifest"]);

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var allResources = appModel.Resources.ToList();
        Assert.NotEmpty(allResources);
    }

    [Fact]
    public void RunMode_DoesNotAddDockerfileAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var dockerAnnotations = resource.Annotations
            .OfType<DockerfileBuilderCallbackAnnotation>()
            .ToList();
        Assert.Empty(dockerAnnotations);
    }

    #endregion
}
