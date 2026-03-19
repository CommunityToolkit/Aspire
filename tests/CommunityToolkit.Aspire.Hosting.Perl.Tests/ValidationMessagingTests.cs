using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class ValidationMessagingTests
{
    [Fact, RequiresWindows]
    public void WithPerlbrewEnvironment_OnWindows_DoesNotRegisterPerlbrewRequiredCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPerlbrewEnvironment("5.38.0", perlbrewRoot: "/home/user/perl5/perlbrew");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

#pragma warning disable ASPIRECOMMAND001
        var requiredCommands = resource.Annotations.OfType<RequiredCommandAnnotation>().ToList();
#pragma warning restore ASPIRECOMMAND001

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
#pragma warning disable ASPIREINTERACTION001
        builder.Services.AddSingleton<IInteractionService>(interactionService);
#pragma warning restore ASPIREINTERACTION001

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
#pragma warning disable ASPIREINTERACTION001
        Assert.Equal(MessageIntent.Warning, interactionService.LastOptions?.Intent);
#pragma warning restore ASPIREINTERACTION001
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

#pragma warning disable ASPIRECOMMAND001
        var perlbrewAnnotation = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .Single(a => a.Command == "perlbrew");

        var context = new RequiredCommandValidationContext("perlbrew", app.Services, CancellationToken.None);
#pragma warning restore ASPIRECOMMAND001
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

#pragma warning disable ASPIRECOMMAND001
        var perlbrewAnnotation = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .Single(a => a.Command == "perlbrew");

        var context = new RequiredCommandValidationContext("perlbrew", app.Services, CancellationToken.None);
#pragma warning restore ASPIRECOMMAND001
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

#pragma warning disable ASPIRECOMMAND001
        var cpanmRequiredCommand = resource.Annotations
            .OfType<RequiredCommandAnnotation>()
            .Single(a => a.Command == "cpanm");

        var context = new RequiredCommandValidationContext("cpanm", app.Services, CancellationToken.None);
#pragma warning restore ASPIRECOMMAND001
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

#pragma warning disable ASPIREINTERACTION001
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
#pragma warning restore ASPIREINTERACTION001
}
