using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithPerlbrewExtensionTests
{
    [Fact, RequiresLinux]
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

    [Fact, RequiresLinux]
    public void WithPerlbrew_ChangesCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrew("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "perl");
        Assert.Equal(expected, resource.Command);
    }

    [Fact, RequiresLinux]
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

    [Fact, RequiresLinux]
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

    [Fact, RequiresLinux]
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

#pragma warning disable ASPIRECOMMAND001
    [Fact, RequiresLinux]
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

#pragma warning restore ASPIRECOMMAND001

    [Fact, RequiresLinux]
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

    [Fact, RequiresLinux]
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

    [Fact, RequiresLinux]
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

    [Fact, RequiresLinux]
    public async Task WithPerlbrewEnvironment_CalledTwice_DoesNotDuplicateEnvironmentCallback()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew")
            .WithPerlbrewEnvironment("5.40.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var envCallbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.Single(resource.Annotations.OfType<PerlbrewResourceEnvironmentAnnotation>());

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var callback in envCallbacks)
        {
            await callback.Callback(context);
        }

        Assert.Equal("perl-5.40.0", envVars["PERLBREW_PERL"]?.ToString());
    }
}
