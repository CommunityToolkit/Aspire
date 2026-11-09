using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCartonDockerfileTests
{
    [Fact]
    public void BuildCartonDockerfile_ProducesMultiStage()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            hasCpanfile: true,
            hasSnapshot: true);
#pragma warning restore ASPIREDOCKERFILEBUILDER001

        Assert.Equal(2, builder.Stages.Count);
    }

    [Fact]
    public void BuildCartonDockerfile_BuildStageNamedBuild()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            hasCpanfile: true,
            hasSnapshot: true);
#pragma warning restore ASPIREDOCKERFILEBUILDER001

        Assert.Equal("build", builder.Stages[0].StageName);
    }

    [Fact]
    public void BuildCartonDockerfile_BuildStageProducesExpectedStatementSequence()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            hasCpanfile: true,
            hasSnapshot: true);
#pragma warning restore ASPIREDOCKERFILEBUILDER001

        string[] expected =
        [
            "DockerfileFromStatement",
            "DockerfileWorkDirStatement",
            "DockerfileRunStatement",        // cpanm Carton
            "DockerfileEnvStatement",        // PERL_CARTON_PATH
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
#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            hasCpanfile: true,
            hasSnapshot: true);
#pragma warning restore ASPIREDOCKERFILEBUILDER001

        string[] expected =
        [
            "DockerfileFromStatement",
            "DockerfileWorkDirStatement",
            "DockerfileCopyFromStatement",   // copy from build stage
            "DockerfileEnvStatement",        // PERL5LIB
            "DockerfileEnvStatement",        // PERL_LOCAL_LIB_ROOT
            "DockerfileEntrypointStatement",
        ];

        var actual = builder.Stages[1].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.Equal(expected, actual);
    }

}
