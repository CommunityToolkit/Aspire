using System.Collections.Concurrent;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

/// <summary>
/// Publishes a Perl app through the manifest publisher and inspects the Docker build artifacts it emits.
/// These tests exercise the real publish pipeline, so they cover whether the generated Dockerfile is used.
/// </summary>
public class PublishDockerfileArtifactsTests
{
    private const string ResourceName = "perl-app";

    [Theory, RequiresDocker]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_WithoutManifest_GeneratedImageRuns(bool useCarton)
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp, includeCpanfile: false);
        File.WriteAllText(Path.Combine(appDir, "app.pl"), "use JSON::PP; exit 0;\n");

        var result = await PublishAsync(temp, appDir, app =>
        {
            if (useCarton)
            {
                app.WithCarton();
            }
        });

        await BuildAndRunImageAsync(result, appDir);
    }

    [Theory, RequiresDocker]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_WithDependencies_GeneratedImageLoadsInstalledModules(bool useCarton)
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);
        File.WriteAllText(Path.Combine(appDir, "cpanfile"), "requires 'Try::Tiny';\n");
        File.WriteAllText(Path.Combine(appDir, "app.pl"),
            useCarton ? "use Try::Tiny; exit 0;\n" : "use Try::Tiny; use Class::Tiny; exit 0;\n");

        var result = await PublishAsync(temp, appDir, app =>
        {
            if (useCarton)
            {
                app.WithCarton().WithProjectDependencies(cartonDeployment: false);
            }
            else
            {
                app.WithPackage("Class::Tiny", skipTest: true).WithLocalLib("vendor/perl5");
            }
        });

        await BuildAndRunImageAsync(result, appDir);
    }

    [Theory, RequiresDocker]
    [InlineData("Makefile.PL")]
    [InlineData("Build.PL")]
    public async Task Publish_WithExecutableManifestLoadingProjectFile_GeneratedImageRuns(string manifestName)
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp, includeCpanfile: false);
        File.WriteAllText(Path.Combine(appDir, "deps.pl"), "+{ 'Try::Tiny' => 0 };\n");
        File.WriteAllText(Path.Combine(appDir, "app.pl"), "use Try::Tiny; exit 0;\n");
        var moduleDir = Directory.CreateDirectory(Path.Combine(appDir, "lib", "Publish")).FullName;
        File.WriteAllText(Path.Combine(moduleDir, "Smoke.pm"), "package Publish::Smoke;\nour $VERSION = '0.01';\n1;\n");
        File.WriteAllText(Path.Combine(appDir, manifestName),
            manifestName == "Makefile.PL"
                ? """
                  use ExtUtils::MakeMaker;
                  my $dependencies = require './deps.pl';
                  WriteMakefile(
                      NAME => 'Publish::Smoke',
                      VERSION => '0.01',
                      PREREQ_PM => $dependencies,
                  );
                  """
                : """
                  use Module::Build;
                  my $dependencies = require './deps.pl';
                  Module::Build->new(
                      module_name => 'Publish::Smoke',
                      dist_version => '0.01',
                      dist_abstract => 'Publish smoke test',
                      dist_author => 'Aspire tests',
                      license => 'perl',
                      requires => $dependencies,
                  )->create_build_script;
                  """);

        var result = await PublishAsync(temp, appDir, app => app.WithLocalLib("vendor/perl5"));

        await BuildAndRunImageAsync(result, appDir);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_WithoutDependencyManifest_OmitsDependencySteps(bool useCarton)
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp, includeCpanfile: false);

        var result = await PublishAsync(temp, appDir, app =>
        {
            if (useCarton)
            {
                app.WithCarton();
            }
        });

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("COPY cpanfile", dockerfile);
        Assert.DoesNotContain("--installdeps", dockerfile);
        Assert.DoesNotContain("RUN carton install", dockerfile);
        Assert.Contains("COPY . .", dockerfile);
        Assert.Contains("ENTRYPOINT [\"perl\"", dockerfile);
    }

    [Theory]
    [InlineData("Makefile.PL", false)]
    [InlineData("Build.PL", false)]
    [InlineData("Makefile.PL", true)]
    [InlineData("Build.PL", true)]
    public async Task Publish_WithExecutableManifest_CopiesProjectBeforeInstalling(string manifestName, bool includeCpanfile)
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp, includeCpanfile);
        File.WriteAllText(Path.Combine(appDir, manifestName), "# dependency manifest\n");

        var result = await PublishAsync(temp, appDir);

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        var copyIndex = dockerfile.IndexOf("COPY . .", StringComparison.Ordinal);
        var installIndex = dockerfile.IndexOf("RUN cpanm --installdeps --notest .", StringComparison.Ordinal);
        Assert.True(copyIndex >= 0, $"Expected application context COPY for {manifestName}.\n{dockerfile}");
        Assert.True(installIndex > copyIndex, $"Expected dependency installation after application context COPY.\n{dockerfile}");
        Assert.Single(dockerfile.Split('\n'), line => line.TrimEnd('\r') == "COPY . .");
    }

    [Fact]
    public async Task Publish_WithCpanfileOnly_InstallsDependenciesBeforeCopyingProject()
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);

        var result = await PublishAsync(temp, appDir);

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        var manifestCopyIndex = dockerfile.IndexOf("COPY cpanfile ./", StringComparison.Ordinal);
        var installIndex = dockerfile.IndexOf("RUN cpanm --installdeps --notest .", StringComparison.Ordinal);
        var projectCopyIndex = dockerfile.IndexOf("COPY . .", StringComparison.Ordinal);
        Assert.True(manifestCopyIndex >= 0 && installIndex > manifestCopyIndex && projectCopyIndex > installIndex,
            $"Expected a manifest-only dependency cache layer before application context COPY.\n{dockerfile}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_WithCartonWithoutDeployment_CopiesSnapshotOnlyWhenPresent(bool hasSnapshot)
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp);
        if (hasSnapshot)
        {
            File.WriteAllText(Path.Combine(appDir, "cpanfile.snapshot"), "# carton snapshot\n");
        }

        var result = await PublishAsync(temp, appDir, app => app.WithCarton().WithProjectDependencies(cartonDeployment: false));

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        Assert.Contains("COPY cpanfile ./", dockerfile);
        Assert.Equal(hasSnapshot, dockerfile.Contains("COPY cpanfile.snapshot ./", StringComparison.Ordinal));
        Assert.Contains("RUN carton install\n", dockerfile.ReplaceLineEndings("\n"));
        Assert.DoesNotContain("carton install --deployment", dockerfile);
    }

    [Theory]
    [InlineData("cpanfile", "cpanfile.snapshot", "carton install")]
    [InlineData("Makefile.PL", "cpanfile", "WithCpanMinus")]
    [InlineData("Build.PL", "cpanfile", "WithCpanMinus")]
    public async Task Publish_WithCartonMissingRequiredFiles_FailsWithActionableError(
        string manifestName, string requiredFile, string remedy)
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp, includeCpanfile: false);
        File.WriteAllText(Path.Combine(appDir, manifestName), "# dependency manifest\n");
        var outputDir = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;
        var builder = DistributedApplication.CreateBuilder(
            ["--operation", "publish", "--publisher", "manifest", "--output-path", outputDir, "--step", "publish-manifest"]);
        using var errorLogger = new PublishErrorLoggerProvider();
        builder.Services.AddLogging(logging => logging.AddProvider(errorLogger));
        builder.AddPerlScript(ResourceName, appDir, "app.pl")
            .WithCarton()
            .WithProjectDependencies(cartonDeployment: true);

        using var app = builder.Build();
        await app.RunAsync(TestContext.Current.CancellationToken);

        Assert.Contains(errorLogger.Exceptions, exception =>
            exception is DistributedApplicationException &&
            exception.Message.Contains(requiredFile, StringComparison.Ordinal) &&
            exception.Message.Contains(remedy, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Publish_WithPackage_InstallsModuleAfterProjectDependencies(bool hasManifest, bool useLocalLib)
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp, includeCpanfile: hasManifest);

        var result = await PublishAsync(temp, appDir, app =>
        {
            app.WithPackage("Foo::Bar", skipTest: true);
            if (useLocalLib)
            {
                app.WithLocalLib("vendor/perl5");
            }
        });

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        var expectedInstall = useLocalLib
            ? "RUN cpanm --local-lib /app/vendor/perl5 --notest Foo::Bar"
            : "RUN cpanm --notest Foo::Bar";
        Assert.Contains(expectedInstall, dockerfile);
        Assert.Equal(hasManifest, dockerfile.Contains("--installdeps", StringComparison.Ordinal));
        if (hasManifest)
        {
            Assert.True(
                dockerfile.IndexOf("--installdeps", StringComparison.Ordinal) <
                dockerfile.IndexOf(expectedInstall, StringComparison.Ordinal),
                $"Expected package installation after project dependencies.\n{dockerfile}");
        }
    }

    [Fact]
    public async Task Publish_WithRepeatedPackage_UsesFirstInstallOptionsOnce()
    {
        using var temp = new TempDirectory();
        var appDir = CreateAppDirectory(temp, includeCpanfile: false);

        var result = await PublishAsync(temp, appDir, app => app
            .WithPackage("Foo::Bar", force: true, skipTest: false)
            .WithPackage("Foo::Bar", force: false, skipTest: true)
            .WithPackage("Baz::Qux", skipTest: true));

        var dockerfile = await File.ReadAllTextAsync(result.DockerfilePath, TestContext.Current.CancellationToken);
        Assert.Single(dockerfile.Split('\n'), line => line.TrimEnd('\r') == "RUN cpanm --force Foo::Bar");
        Assert.DoesNotContain("--notest Foo::Bar", dockerfile);
        Assert.Contains("RUN cpanm --notest Baz::Qux", dockerfile);
    }

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

    private static string CreateAppDirectory(TempDirectory temp, bool includeCpanfile = true)
    {
        var appDir = Directory.CreateDirectory(Path.Combine(temp.Path, "app")).FullName;
        File.WriteAllText(Path.Combine(appDir, "app.pl"), "print \"ok\\n\";\n");
        if (includeCpanfile)
        {
            File.WriteAllText(Path.Combine(appDir, "cpanfile"), "requires 'JSON::PP';\n");
        }
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

    private static async Task BuildAndRunImageAsync(PublishResult result, string appDir)
    {
        var imageName = $"aspire-perl-publish-test:{Guid.NewGuid():N}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));

        await ProcessTestUtilities.RunProcessAsync("docker",
            ["build", "--progress", "plain", "--file", result.DockerfilePath, "--tag", imageName, appDir],
            appDir, timeout.Token);

        try
        {
            await ProcessTestUtilities.RunProcessAsync("docker", ["run", "--rm", imageName],
                appDir, timeout.Token);
        }
        finally
        {
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await ProcessTestUtilities.RunProcessAsync("docker", ["image", "rm", "--force", imageName],
                appDir, cleanupTimeout.Token);
        }
    }

    private sealed record PublishResult(string OutputDirectory, string DockerfilePath);

    private sealed class PublishErrorLoggerProvider : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<Exception> Exceptions { get; } = new();

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error && exception is not null)
            {
                Exceptions.Enqueue(exception);
            }
        }

        public void Dispose() { }
    }
}
