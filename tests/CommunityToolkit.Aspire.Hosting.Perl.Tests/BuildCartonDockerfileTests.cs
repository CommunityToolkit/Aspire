using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCartonDockerfileTests
{
    [Fact]
    public void BuildCartonDockerfile_ProducesMultiStage()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        Assert.Equal(2, builder.Stages.Count);
    }

    [Fact]
    public void BuildCartonDockerfile_BuildStageNamedBuild()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        Assert.Equal("build", builder.Stages[0].StageName);
    }

    [Fact]
    public void BuildCartonDockerfile_BuildStageProducesExpectedStatementSequence()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        string[] expected =
        [
            "DockerfileFromStatement",
            "DockerfileWorkDirStatement",
            "DockerfileRunStatement",        // cpanm Carton
            "DockerfileCopyStatement",       // cpanfile
            "DockerfileCopyStatement",       // cpanfile.snapshot
            "DockerfileRunStatement",        // carton install
            "DockerfileCopyStatement",       // application source
        ];

        var actual = builder.Stages[0].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildCartonDockerfile_RuntimeStageProducesExpectedStatementSequence()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        string[] expected =
        [
            "DockerfileFromStatement",
            "DockerfileWorkDirStatement",
            "DockerfileCopyFromStatement",   // copy from build stage
            "DockerfileEntrypointStatement",
        ];

        var actual = builder.Stages[1].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.Equal(expected, actual);
    }

}
