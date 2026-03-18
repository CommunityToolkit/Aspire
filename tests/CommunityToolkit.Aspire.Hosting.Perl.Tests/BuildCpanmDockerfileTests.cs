using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCpanmDockerfileTests
{
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
    [Fact]
    public void BuildCpanmDockerfile_UsesSingleStage()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

        Assert.Single(builder.Stages);
    }

    [Fact]
    public void BuildCpanmDockerfile_StageHasStatements()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

        var stage = builder.Stages[0];
        Assert.NotEmpty(stage.Statements);
    }

    [Fact]
    public void BuildCpanmDockerfile_CopiesDependencyManifestBeforeSource()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

        var statements = builder.Stages[0].Statements;
        var copyIndexes = statements
            .Select((statement, index) => (statement, index))
            .Where(item => item.statement.GetType().Name == "DockerfileCopyStatement")
            .Select(item => item.index)
            .ToList();

        Assert.Equal(2, copyIndexes.Count);
        Assert.True(copyIndexes[0] < copyIndexes[1]);
    }

    [Fact]
    public void BuildCpanmDockerfile_InstallsDependenciesAfterManifestCopy()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

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
        Assert.True(copyIndexes[0] < runIndexes[1]);
        Assert.True(runIndexes[1] < copyIndexes[1]);
    }

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
}
