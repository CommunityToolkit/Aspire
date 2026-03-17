using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIRECOMMAND001
#pragma warning disable ASPIREINTERACTION001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlbrewEnvironmentTests
{
    #region PerlbrewEnvironment Utility Class

    [Fact, RequiresLinux]
    public void NormalizeVersion_PrefixesPerlWhenMissing()
    {
        var result = PerlbrewEnvironment.NormalizeVersion("5.38.0");

        Assert.Equal("perl-5.38.0", result);
    }

    [Fact, RequiresLinux]
    public void NormalizeVersion_KeepsExistingPrefix()
    {
        var result = PerlbrewEnvironment.NormalizeVersion("perl-5.38.0");

        Assert.Equal("perl-5.38.0", result);
    }

    [Fact, RequiresLinux]
    public void NormalizeVersion_NormalizesUpperCasePrefixToLower()
    {
        // Perlbrew installs under a lowercase directory name regardless of input casing.
        // NormalizeVersion must always emit a lowercase "perl-" prefix.
        var result = PerlbrewEnvironment.NormalizeVersion("Perl-5.38.0");

        Assert.Equal("perl-5.38.0", result);
    }

    [Fact, RequiresLinux]
    public void ResolvePerlbrewRoot_UsesExplicitValue()
    {
        var result = PerlbrewEnvironment.ResolvePerlbrewRoot("/custom/perlbrew");

        Assert.Equal("/custom/perlbrew", result);
    }

    [Fact, RequiresLinux]
    public void ResolvePerlbrewRoot_FallsBackToDefault()
    {
        // When no explicit root and no env var, falls back to ~/perl5/perlbrew
        var result = PerlbrewEnvironment.ResolvePerlbrewRoot(null);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "perl5", "perlbrew");

        Assert.Equal(expected, result);
    }

    [Fact, RequiresLinux]
    public void GetExecutable_ReturnsCorrectPath()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var perlPath = env.GetExecutable("perl");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "perl");
        Assert.Equal(expected, perlPath);
    }

    [Fact, RequiresLinux]
    public void GetExecutable_ResolveCpanm()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var cpanmPath = env.GetExecutable("cpanm");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "cpanm");
        Assert.Equal(expected, cpanmPath);
    }

    [Fact, RequiresLinux]
    public void BinPath_IsCorrect()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin");
        Assert.Equal(expected, env.BinPath);
    }

    [Fact, RequiresLinux]
    public void VersionPath_IsCorrect()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0");
        Assert.Equal(expected, env.VersionPath);
    }

    [Fact, RequiresLinux]
    public void Properties_ReturnConstructorValues()
    {
        var env = new PerlbrewEnvironment("/opt/perlbrew", "perl-5.40.0");

        Assert.Equal("/opt/perlbrew", env.PerlbrewRoot);
        Assert.Equal("perl-5.40.0", env.Version);
    }

    #endregion

    #region WithPerlbrewEnvironment Extension

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

    #endregion

    #region Installer Integration

    [Fact, RequiresLinux]
    public void WithPerlbrewEnvironmentAndCpanm_InstallerGetsPerlbrewAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew")
            .WithCpanMinus()
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // The parent resource should have the perlbrew annotation
        Assert.True(resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var annotation));
        Assert.NotNull(annotation.Environment);
    }

    [Fact, RequiresLinux]
    public void WithPerlbrewEnvironmentAndCpanm_DoesNotAddCpanmBootstrapInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/tmp/ctaspire-missing-perlbrew-root")
            .WithCpanMinus()
            .WithPackage("OpenTelemetry::SDK");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.DoesNotContain(
            appModel.Resources.OfType<ExecutableResource>(),
            resource => resource.Name == "perl-app-perlbrew-cpanm-installer");
    }

    [Fact, RequiresLinux]
    public void WithPerlbrewEnvironmentAndCpanm_ModuleInstallerDoesNotWaitForBootstrapInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/tmp/ctaspire-missing-perlbrew-root")
            .WithCpanMinus()
            .WithPackage("OpenTelemetry::SDK");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var moduleInstaller = appModel.Resources
            .OfType<PerlModuleInstallerResource>()
            .Single(r => r.Name.Contains("OpenTelemetry", StringComparison.Ordinal));

        var appResource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());
        PerlAppResourceBuilderExtensions.SetupDependencies(builder, appResource);

        Assert.DoesNotContain(
            moduleInstaller.Annotations.OfType<WaitAnnotation>(),
            wait => wait.Resource.Name == "perl-app-perlbrew-cpanm-installer" && wait.WaitType == WaitType.WaitForCompletion);
    }

    #endregion

    #region Validation Messaging

    [Fact, RequiresWindows]
    public void WithPerlbrewEnvironment_OnWindows_DoesNotRegisterPerlbrewRequiredCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var requiredCommands = resource.Annotations.OfType<RequiredCommandAnnotation>().ToList();

        // Windows pathway uses direct interaction service in startup events, not command validation.
        Assert.Equal(2, requiredCommands.Count);
        Assert.Contains(requiredCommands, a => a.Command == "perl");
        Assert.Contains(requiredCommands, a => a.Command == "cpan");
        Assert.DoesNotContain(requiredCommands, a => a.Command == "perlbrew");
        Assert.DoesNotContain(requiredCommands, a => a.HelpLink == "https://github.com/stevieb9/berrybrew");
    }

    [Fact, RequiresWindows]
    public async Task WithPerlbrewEnvironment_OnWindows_PromptsExpectedNotification()
    {
        const string expectedMessage =
            "Perlbrew is unsupported on Windows. " +
            "The recommendation is to use Berrybrew. " +
            "Support for Berrybrew is on the roadmap for a future release.";
        const string expectedLink = "https://github.com/stevieb9/berrybrew";

        var builder = DistributedApplication.CreateBuilder();
        var interactionService = new RecordingInteractionService();
        builder.Services.AddSingleton<IInteractionService>(interactionService);

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, app.Services), CancellationToken.None));

        Assert.Equal(expectedMessage, exception.Message);
        Assert.True(interactionService.PromptNotificationCalled);
        Assert.Equal("Perlbrew on Windows", interactionService.LastTitle);
        Assert.Equal(expectedMessage, interactionService.LastMessage);
        Assert.Equal(MessageIntent.Warning, interactionService.LastOptions?.Intent);
        Assert.Equal("Installation instructions", interactionService.LastOptions?.LinkText);
        Assert.Equal(expectedLink, interactionService.LastOptions?.LinkUrl);
    }

    [Fact, RequiresLinux]
    public async Task WithPerlbrewEnvironment_WhenVersionMissing_ShowsInstallInstructions()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/tmp/ctaspire-missing-perlbrew-root");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var perlbrewAnnotation = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .Single(a => a.Command == "perlbrew");

        var context = new RequiredCommandValidationContext("perlbrew", app.Services, CancellationToken.None);
        var result = await perlbrewAnnotation.ValidationCallback!(context);

        Assert.False(result.IsValid);
        Assert.Equal("https://perlbrew.pl/", perlbrewAnnotation.HelpLink);
        Assert.Contains("Install with: perlbrew install perl-5.38.0", result.ValidationMessage);
        Assert.Contains("sudo apt install perlbrew", result.ValidationMessage);
        Assert.Contains("perlbrew website", result.ValidationMessage);
    }

    [Fact, RequiresLinux]
    public async Task WithPerlbrewEnvironment_WhenVersionMissing_ValidationDoesNotBypassInstallInstructions()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/tmp/ctaspire-missing-perlbrew-root");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var perlbrewAnnotation = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .Single(a => a.Command == "perlbrew");

        var context = new RequiredCommandValidationContext("perlbrew", app.Services, CancellationToken.None);
        var result = await perlbrewAnnotation.ValidationCallback!(context);

        Assert.False(result.IsValid);
        Assert.Contains("Install with: perlbrew install perl-5.38.0", result.ValidationMessage);
    }

    [Fact, RequiresLinux]
    public async Task WithPerlbrewEnvironmentAndCpanMinus_ValidationFailsWhenCpanmIsMissing()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/tmp/ctaspire-missing-perlbrew-root")
            .WithCpanMinus();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var cpanmRequiredCommand = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .Single(a => a.Command == "cpanm");

        var context = new RequiredCommandValidationContext("cpanm", app.Services, CancellationToken.None);
        var result = await cpanmRequiredCommand.ValidationCallback!(context);

        Assert.False(result.IsValid);
        Assert.Contains("perlbrew install-cpanm", result.ValidationMessage);
    }

    [Fact, RequiresLinux]
    public void WithPerlbrewEnvironment_DoesNotAddVersionInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/tmp/ctaspire-missing-perlbrew-root");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.DoesNotContain(
            appModel.Resources.OfType<ExecutableResource>(),
            resource => resource.Name == "perl-app-perl-5-38-0-perlbrew-installer");
    }

    #endregion

    private sealed class RecordingInteractionService : IInteractionService
    {
        public bool IsAvailable => true;

        public bool PromptNotificationCalled { get; private set; }

        public string? LastTitle { get; private set; }

        public string? LastMessage { get; private set; }

        public NotificationInteractionOptions? LastOptions { get; private set; }

        public Task<InteractionResult<bool>> PromptConfirmationAsync(
            string title,
            string message,
            MessageBoxInteractionOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromException<InteractionResult<bool>>(new NotSupportedException());

        public Task<InteractionResult<bool>> PromptMessageBoxAsync(
            string title,
            string message,
            MessageBoxInteractionOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromException<InteractionResult<bool>>(new NotSupportedException());

        public Task<InteractionResult<InteractionInput>> PromptInputAsync(
            string title,
            string? message,
            string inputLabel,
            string placeHolder,
            InputsDialogInteractionOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromException<InteractionResult<InteractionInput>>(new NotSupportedException());

        public Task<InteractionResult<InteractionInput>> PromptInputAsync(
            string title,
            string? message,
            InteractionInput input,
            InputsDialogInteractionOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromException<InteractionResult<InteractionInput>>(new NotSupportedException());

        public Task<InteractionResult<InteractionInputCollection>> PromptInputsAsync(
            string title,
            string? message,
            IReadOnlyList<InteractionInput> inputs,
            InputsDialogInteractionOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromException<InteractionResult<InteractionInputCollection>>(new NotSupportedException());

        public Task<InteractionResult<bool>> PromptNotificationAsync(
            string title,
            string message,
            NotificationInteractionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            PromptNotificationCalled = true;
            LastTitle = title;
            LastMessage = message;
            LastOptions = options;

            return Task.FromResult<InteractionResult<bool>>(null!);
        }
    }
}
