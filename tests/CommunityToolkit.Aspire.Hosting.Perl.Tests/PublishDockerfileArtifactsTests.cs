using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

/// <summary>
/// Publishes a Perl app through the manifest publisher and inspects the Docker build artifacts it emits.
/// These tests exercise the real publish pipeline, so they cover whether the generated Dockerfile is used.
/// </summary>
public class PublishDockerfileArtifactsTests
{
    private const string ResourceName = "perl-app";

    [Fact]
    public async Task Publish_WithoutAuthoredDockerfile_UsesGeneratedDockerfile()
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);

        var result = await PublishAsync(temp, appDir);

        Assert.NotEqual(Path.Combine(appDir, "Dockerfile"), result.DockerfilePath, StringComparer.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.DockerfilePath), $"Expected generated Dockerfile at '{result.DockerfilePath}'.");
        Assert.False(File.Exists(Path.Combine(appDir, "Dockerfile")));

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        Assert.Contains("FROM perl:5-slim", dockerfile);
        Assert.Contains("cpanm --installdeps --notest .", dockerfile);
    }

    [Fact]
    public async Task Publish_WithAuthoredDockerfile_UsesAuthoredDockerfileUnchanged()
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);
        const string authored = "FROM scratch\n";
        File.WriteAllText(Path.Combine(appDir, "Dockerfile"), authored);

        var result = await PublishAsync(temp, appDir);

        Assert.Equal(Path.Combine(appDir, "Dockerfile"), result.DockerfilePath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(authored, await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(result.DockerfilePath + ".dockerignore"));
        Assert.Empty(Directory.GetFiles(result.OutputDirectory, "*.dockerignore", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Publish_EmitsBuildContextIgnoreNextToGeneratedDockerfile()
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);

        var result = await PublishAsync(temp, appDir);

        var ignorePath = result.DockerfilePath + ".dockerignore";
        Assert.True(File.Exists(ignorePath), $"Expected generated ignore file at '{ignorePath}'.");

        var ignore = await File.ReadAllTextAsync(ignorePath, TestContext.Current.CancellationToken);
        Assert.Equal(PerlAppResourceBuilderExtensions.BuildContextIgnoreContent(localLibPath: null), ignore);

        var rules = ReadRules(ignore);
        Assert.Contains("local", rules);
        Assert.Contains(".git", rules);
        Assert.Contains("blib", rules);
        Assert.Contains("_build", rules);
    }

    [Fact]
    public async Task Publish_WithCarton_ExcludesHostLocalDirectoryFromBuildContext()
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);
        File.WriteAllText(Path.Combine(appDir, "cpanfile.snapshot"), "# carton snapshot\n");

        var result = await PublishAsync(temp, appDir, app => app.WithCarton());

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        Assert.Contains("carton install --deployment", dockerfile);

        var rules = ReadRules(await File.ReadAllTextAsync(result.DockerfilePath + ".dockerignore", TestContext.Current.CancellationToken));
        Assert.Contains("local", rules);
    }

    [Fact]
    public async Task Publish_WithLocalLib_ExcludesConfiguredLocalLibPathFromBuildContext()
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);

        var result = await PublishAsync(temp, appDir, app => app.WithLocalLib("vendor/perl5"));

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        Assert.Contains("--local-lib /app/vendor/perl5", dockerfile);

        var rules = ReadRules(await File.ReadAllTextAsync(result.DockerfilePath + ".dockerignore", TestContext.Current.CancellationToken));
        Assert.Contains("local", rules);
        Assert.Contains("vendor/perl5", rules);
    }

    [Fact]
    public async Task Publish_WithAuthoredDockerignore_DoesNotEmitGeneratedIgnore()
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);
        File.WriteAllText(Path.Combine(appDir, ".dockerignore"), "*.md\n");

        var result = await PublishAsync(temp, appDir);

        Assert.True(File.Exists(result.DockerfilePath), "The Dockerfile should still be generated.");
        Assert.False(File.Exists(result.DockerfilePath + ".dockerignore"));
        Assert.Equal("*.md\n", File.ReadAllText(Path.Combine(appDir, ".dockerignore")));
    }

    private static string CreateAppDirectory(TempDirectory temp)
    {
        var appDir = Directory.CreateDirectory(Path.Combine(temp.Path, "app")).FullName;
        File.WriteAllText(Path.Combine(appDir, "app.pl"), "print \"ok\\n\";\n");
        File.WriteAllText(Path.Combine(appDir, "cpanfile"), "requires 'JSON::PP';\n");
        return appDir;
    }

    private static async Task<PublishResult> PublishAsync(
        TempDirectory temp,
        string appDir,
        Action<IResourceBuilder<PerlAppResource>>? configure = null)
    {
        var outputDir = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;

        var builder = DistributedApplication.CreateBuilder(
            ["--operation", "publish", "--publisher", "manifest", "--output-path", outputDir, "--step", "publish-manifest"]);

        var app = builder.AddPerlScript(ResourceName, appDir, "app.pl");
        configure?.Invoke(app);

        using (var distributedApp = builder.Build())
        {
            await distributedApp.RunAsync(TestContext.Current.CancellationToken);
        }

        var manifestPath = Path.Combine(outputDir, "aspire-manifest.json");
        Assert.True(File.Exists(manifestPath), $"Expected manifest at '{manifestPath}'.");

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, TestContext.Current.CancellationToken));
        var relativeDockerfile = manifest.RootElement
            .GetProperty("resources")
            .GetProperty(ResourceName)
            .GetProperty("build")
            .GetProperty("dockerfile")
            .GetString();

        Assert.False(string.IsNullOrEmpty(relativeDockerfile));

        return new PublishResult(outputDir, Path.GetFullPath(relativeDockerfile, outputDir));
    }

    private static string[] ReadRules(string ignoreContent) =>
        ignoreContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record PublishResult(string OutputDirectory, string DockerfilePath);
}
