using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIRECOMMAND001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlbrewEnvironmentTests
{
    // --- PerlbrewEnvironment utility class tests ---

    [Fact]
    public void NormalizeVersion_PrefixesPerlWhenMissing()
    {
        var result = PerlbrewEnvironment.NormalizeVersion("5.38.0");

        Assert.Equal("perl-5.38.0", result);
    }

    [Fact]
    public void NormalizeVersion_KeepsExistingPrefix()
    {
        var result = PerlbrewEnvironment.NormalizeVersion("perl-5.38.0");

        Assert.Equal("perl-5.38.0", result);
    }

    [Fact]
    public void NormalizeVersion_IsCaseInsensitive()
    {
        var result = PerlbrewEnvironment.NormalizeVersion("Perl-5.38.0");

        Assert.Equal("Perl-5.38.0", result);
    }

    [Fact]
    public void ResolvePerlbrewRoot_UsesExplicitValue()
    {
        var result = PerlbrewEnvironment.ResolvePerlbrewRoot("/custom/perlbrew");

        Assert.Equal("/custom/perlbrew", result);
    }

    [Fact]
    public void ResolvePerlbrewRoot_FallsBackToDefault()
    {
        // When no explicit root and no env var, falls back to ~/perl5/perlbrew
        var result = PerlbrewEnvironment.ResolvePerlbrewRoot(null);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "perl5", "perlbrew");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetExecutable_ReturnsCorrectPath()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var perlPath = env.GetExecutable("perl");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "perl");
        Assert.Equal(expected, perlPath);
    }

    [Fact]
    public void GetExecutable_ResolveCpanm()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var cpanmPath = env.GetExecutable("cpanm");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "cpanm");
        Assert.Equal(expected, cpanmPath);
    }

    [Fact]
    public void BinPath_IsCorrect()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin");
        Assert.Equal(expected, env.BinPath);
    }

    [Fact]
    public void VersionPath_IsCorrect()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0");
        Assert.Equal(expected, env.VersionPath);
    }

    [Fact]
    public void Properties_ReturnConstructorValues()
    {
        var env = new PerlbrewEnvironment("/opt/perlbrew", "perl-5.40.0");

        Assert.Equal("/opt/perlbrew", env.PerlbrewRoot);
        Assert.Equal("perl-5.40.0", env.Version);
    }

    // --- WithPerlbrewEnvironment extension method tests ---

    [Fact]
    public void WithPerlbrewEnvironment_ChangesCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "perl");
        Assert.Equal(expected, resource.Command);
    }

    [Fact]
    public void WithPerlbrewEnvironment_AcceptsFullVersionName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("perl-5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "perl");
        Assert.Equal(expected, resource.Command);
    }

    [Fact]
    public void WithPerlbrewEnvironment_AttachesAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var annotation));
        Assert.Equal("perl-5.38.0", annotation.Name);
        Assert.Equal("perlbrew", annotation.PerlbrewPath);
        Assert.NotNull(annotation.Environment);
    }

    [Fact]
    public void WithPerlbrewEnvironment_AnnotationHasResolvedEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/opt/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var annotation));

        var env = annotation.Environment!;
        Assert.Equal("/opt/perlbrew", env.PerlbrewRoot);
        Assert.Equal("perl-5.38.0", env.Version);
        var expectedBinPath = Path.Combine("/opt/perlbrew", "perls", "perl-5.38.0", "bin");
        Assert.Equal(expectedBinPath, env.BinPath);
    }

    [Fact]
    public void WithPerlbrewEnvironment_AddsPerlbrewRequiredCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotations = resource.Annotations.OfType<RequiredCommandAnnotation>().ToList();

        // Should have "perl" (from AddPerlAppCore), "cpan" (from AddPerlAppCore), and "perlbrew" (from WithPerlbrewEnvironment)
        Assert.Equal(3, annotations.Count);
        Assert.Contains(annotations, a => a.Command == "perl");
        Assert.Contains(annotations, a => a.Command == "cpan");
        Assert.Contains(annotations, a => a.Command == "perlbrew" && a.HelpLink == "https://perlbrew.pl/");
    }

    [Fact]
    public async Task WithPerlbrewEnvironment_SetsEnvironmentVariables()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // Collect all environment callbacks and execute them
        var envCallbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);

        foreach (var callback in envCallbacks)
        {
            await callback.Callback(context);
        }

        Assert.Equal("/home/user/perl5/perlbrew", envVars["PERLBREW_ROOT"]?.ToString());
        Assert.Equal("perl-5.38.0", envVars["PERLBREW_PERL"]?.ToString());
        var expectedHome = Path.Combine("/home/user/perl5/perlbrew", ".perlbrew");
        Assert.Equal(expectedHome, envVars["PERLBREW_HOME"]?.ToString());
    }

    [Fact]
    public async Task WithPerlbrewEnvironment_PrependsBinToPath()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var envCallbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);

        foreach (var callback in envCallbacks)
        {
            await callback.Callback(context);
        }

        var path = envVars["PATH"]?.ToString();
        Assert.NotNull(path);
        var expectedBinPath = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin");
        Assert.StartsWith(expectedBinPath, path);
    }

    [Fact]
    public void WithPerlbrewEnvironment_ReplacesAnnotationOnSecondCall()
    {
        var builder = DistributedApplication.CreateBuilder();

        var resourceBuilder = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew")
            .WithPerlbrewEnvironment("5.40.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // Should have only one PerlbrewEnvironmentAnnotation (the second call replaces the first)
        var annotations = resource.Annotations.OfType<PerlbrewEnvironmentAnnotation>().ToList();
        Assert.Single(annotations);
        Assert.Equal("perl-5.40.0", annotations[0].Name);

        // Command should reflect the latest version
        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.40.0", "bin", "perl");
        Assert.Equal(expected, resource.Command);
    }

    // --- Installer integration tests ---

    [Fact]
    public void WithPerlbrewEnvironmentAndCpanm_InstallerGetsPerlbrewAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew")
            .WithCpanm("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // The parent resource should have the perlbrew annotation
        Assert.True(resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var annotation));
        Assert.NotNull(annotation.Environment);
    }

    // --- Windows berrybrew warning tests ---

    [Fact]
    public async Task WithPerlbrewEnvironment_OnWindows_ValidationWarnsAboutBerrybrew()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var perlbrewAnnotation = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .Single(a => a.Command == "perlbrew");

        Assert.NotNull(perlbrewAnnotation.ValidationCallback);

        var context = new RequiredCommandValidationContext("perlbrew", app.Services, CancellationToken.None);
        var result = await perlbrewAnnotation.ValidationCallback(context);

        if (OperatingSystem.IsWindows())
        {
            Assert.False(result.IsValid);
            Assert.Contains("Berrybrew", result.ValidationMessage);
            Assert.Contains("not supported on Windows", result.ValidationMessage);
        }
        else
        {
            // On non-Windows, it should check if the perlbrew perl exists (it won't in test environments)
            Assert.False(result.IsValid);
            Assert.Contains("not installed", result.ValidationMessage);
        }
    }
}
