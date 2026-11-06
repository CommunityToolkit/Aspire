using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCpanmDockerfileLocalLibTests
{
    [Fact]
    public void BuildCpanmDockerfile_WithLocalLib_ProducesExpectedStatementSequence()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var builder = new DockerfileBuilder();

        builder.BuildCpanmDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            dependencyFiles: ["cpanfile"],
            localLibPath: "local");
#pragma warning restore ASPIREDOCKERFILEBUILDER001

        string[] expected =
        [
            "DockerfileFromStatement",
            "DockerfileWorkDirStatement",
            "DockerfileRunStatement",        // install cpanm
            "DockerfileCopyStatement",       // cpanfile
            "DockerfileRunStatement",        // cpanm --local-lib --installdeps
            "DockerfileCopyStatement",       // application source
            "DockerfileEnvStatement",        // PERL5LIB
            "DockerfileEnvStatement",        // PERL_LOCAL_LIB_ROOT
            "DockerfileEntrypointStatement",
        ];

        var actual = builder.Stages[0].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildCpanmDockerfile_WithoutLocalLib_HasNoEnvStatements()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var builder = new DockerfileBuilder();

        builder.BuildCpanmDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            dependencyFiles: ["cpanfile"],
            localLibPath: null);
#pragma warning restore ASPIREDOCKERFILEBUILDER001

        var actual = builder.Stages[0].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.DoesNotContain("DockerfileEnvStatement", actual);
    }

    [Fact]
    public async Task BuildCpanmDockerfile_WithPlainLocalLibPath_RendersUnquotedPaths()
    {
        var dockerfile = await RenderCpanmDockerfileAsync("vendor/perl5");

        Assert.Contains("RUN cpanm --local-lib /app/vendor/perl5 --installdeps --notest .\n", dockerfile);
        Assert.Contains("ENV PERL5LIB=/app/vendor/perl5/lib/perl5\n", dockerfile);
        Assert.Contains("ENV PERL_LOCAL_LIB_ROOT=/app/vendor/perl5\n", dockerfile);
        Assert.Contains("ENTRYPOINT [\"perl\",\"-I/app/vendor/perl5/lib/perl5\",\"app.pl\"]", dockerfile);
    }

    [Theory]
    [InlineData("./vendor/perl5/")]
    [InlineData(".\\vendor\\perl5")]
    public async Task BuildCpanmDockerfile_WithUnnormalizedLocalLibPath_RendersNormalizedPath(string localLibPath)
    {
        var dockerfile = await RenderCpanmDockerfileAsync(localLibPath);

        Assert.Contains("RUN cpanm --local-lib /app/vendor/perl5 --installdeps --notest .\n", dockerfile);
        Assert.Contains("ENV PERL_LOCAL_LIB_ROOT=/app/vendor/perl5\n", dockerfile);
    }

    [Fact]
    public async Task BuildCpanmDockerfile_WithSpecialCharactersInLocalLibPath_QuotesPaths()
    {
        var dockerfile = await RenderCpanmDockerfileAsync("my lib/it's $HOME \"x\"");

        Assert.Contains("RUN cpanm --local-lib '/app/my lib/it'\\''s $HOME \"x\"' --installdeps --notest .\n", dockerfile);
        Assert.Contains("ENV PERL5LIB=\"/app/my lib/it's \\$HOME \\\"x\\\"/lib/perl5\"\n", dockerfile);
        Assert.Contains("ENV PERL_LOCAL_LIB_ROOT=\"/app/my lib/it's \\$HOME \\\"x\\\"\"\n", dockerfile);
        Assert.Contains("ENTRYPOINT [\"perl\",\"-I/app/my lib/it's $HOME \\\"x\\\"/lib/perl5\",\"app.pl\"]", dockerfile);
    }

    [Fact]
    public void BuildCpanmDockerfile_WithControlCharactersInLocalLibPath_Throws()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var builder = new DockerfileBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.BuildCpanmDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            dependencyFiles: ["cpanfile"],
            localLibPath: "vendor\nRUN echo injected"));
#pragma warning restore ASPIREDOCKERFILEBUILDER001
    }

    private static async Task<string> RenderCpanmDockerfileAsync(string localLibPath)
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001
        var builder = new DockerfileBuilder();

        builder.BuildCpanmDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            dependencyFiles: ["cpanfile"],
            localLibPath);

        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, leaveOpen: true))
        {
            await builder.WriteAsync(writer, TestContext.Current.CancellationToken);
        }
#pragma warning restore ASPIREDOCKERFILEBUILDER001

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }

}
