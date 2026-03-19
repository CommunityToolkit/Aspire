using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCpanmDockerfileTests
{
    [Fact]
    public void BuildCpanmDockerfile_UsesSingleStage()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        Assert.Single(builder.Stages);
    }

    [Fact]
    public void BuildCpanmDockerfile_StageHasStatements()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        var stage = builder.Stages[0];
        Assert.NotEmpty(stage.Statements);
    }

    [Fact]
    public void BuildCpanmDockerfile_CopiesDependencyManifestBeforeSource()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        var statements = builder.Stages[0].Statements;
        var copyIndexes = statements
            .Select((statement, index) => (statement, index))
            .Where(item => item.statement.GetType().Name == "DockerfileCopyStatement")
            .Select(item => item.index)
            .ToList();

        Assert.Equal(2, copyIndexes.Count);
        Assert.True(copyIndexes[0] < copyIndexes[1],
            $"Dependency manifest COPY (index {copyIndexes[0]}) should precede source COPY (index {copyIndexes[1]})");
    }

    [Fact]
    public void BuildCpanmDockerfile_InstallsDependenciesAfterManifestCopy()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        var statements = builder.Stages[0].Statements;
        var copyIndexes = statements
            .Select((statement, index) => (statement, index))
            .Where(item => item.statement.GetType().Name == "DockerfileCopyStatement")
            .Select(item => item.index)
            .ToList();
        var runIndexes = statements
            .Select((statement, index) => (statement, index))
            .Where(item => item.statement.GetType().Name == "DockerfileRunStatement")
            .Select(item => item.index)
            .ToList();

        Assert.Equal(2, copyIndexes.Count);
        Assert.Equal(2, runIndexes.Count);
        Assert.True(copyIndexes[0] < runIndexes[1],
            $"Manifest COPY (index {copyIndexes[0]}) should precede install RUN (index {runIndexes[1]})");
        Assert.True(runIndexes[1] < copyIndexes[1],
            $"Install RUN (index {runIndexes[1]}) should precede source COPY (index {copyIndexes[1]})");
    }

}
