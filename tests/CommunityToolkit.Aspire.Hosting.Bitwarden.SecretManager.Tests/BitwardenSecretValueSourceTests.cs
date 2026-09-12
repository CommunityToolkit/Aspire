// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001, ASPIREPIPELINES002, ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretValueSourceTests
{
    [Fact]
    public async Task ReferenceSecret_PreservesParameterOverrideWithoutEvaluatingSource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var bitwarden = AddManager(builder);
        var source = new MutableValueSource { Value = "fresh-output" };
        var expression = ReferenceExpression.Create($"{source}");
        builder.Configuration["Parameters:bitwarden-output"] = "previous-deployment";

        var secret = bitwarden.AddSecret("output", expression, remoteName: "Reporting DSN");

        Assert.Equal(0, source.Calls);
        Assert.Equal("previous-deployment", await secret.Resource.GetValueAsync(default));
        Assert.Equal(0, source.Calls);
        Assert.Equal("bitwarden-output", secret.Resource.Name);
        Assert.Equal("Reporting DSN", secret.Resource.RemoteName);
        Assert.True(secret.Resource.Secret);
        Assert.True(secret.Resource.IsManaged);
        Assert.Contains(secret.Resource, bitwarden.Resource.ReferenceSecrets);
        Assert.Same(expression, secret.Resource.ValueProvider.GetSourceExpression(secret.Resource));
        Assert.Contains(expression, ((IValueWithReferences)secret.Resource).References);
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, secret.Resource.Annotations);
        Assert.Equal("{bitwarden.secrets.Reporting DSN}", ((IManifestExpressionProvider)secret.Resource).ValueExpression);
    }

    [Fact]
    public async Task ReferencePreparation_ReplacesBoundValuesAndStoresOneContextSnapshot()
    {
        var builder = DistributedApplication.CreateBuilder();
        var bitwarden = AddManager(builder);
        var source = new MutableValueSource { Value = "fresh-output" };
        var secret = bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        secret.Resource.InitializeWaitForValue();
        secret.Resource.ResolveWaitForValue("old-parameter");
        bitwarden.Resource.BindResolvedSecret(Guid.NewGuid(), "output", "old-remote-value");
        var caller = new ContainerResource("consumer");

        await secret.Resource.ValueProvider.PrepareForWriteAsync(secret.Resource, new ValueProviderContext { Caller = caller }, default);
        var value = await ((IValueProvider)secret.Resource).GetValueAsync(new ValueProviderContext { Caller = caller }, default);

        Assert.Equal("fresh-output", value);
        Assert.Same(caller, source.Caller);
        source.Value = "next-output";
        Assert.Equal("fresh-output", await secret.Resource.GetValueAsync(default));
        await secret.Resource.ValueProvider.PrepareForWriteAsync(secret.Resource, new ValueProviderContext { Caller = caller }, default);
        Assert.Equal("next-output", await ((IValueProvider)secret.Resource).GetValueAsync(default));
        Assert.Equal("next-output", await secret.Resource.GetValueAsync(default));
    }

    [Fact]
    public async Task ExplicitSources_SkipBothUpstreamSyncPhases_WithoutResolvingInputsOrAuthenticating()
    {
        var builder = DistributedApplication.CreateBuilder();
        var bitwarden = AddManager(builder);
        var source = new MutableValueSource { Failure = new InvalidOperationException("Not provisioned yet") };
        bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        var provisioner = new BitwardenSecretManagerProvisioner(new UnexpectedProviderFactory());
        using var services = new ServiceCollection().BuildServiceProvider();

        await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, services, NullLogger.Instance, default);
        await provisioner.SyncMissingManagedSecretValuesAsync(bitwarden.Resource, services, NullLogger.Instance, default);

        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task MixedManagedSecrets_PreSyncsOnlyParameterInputs()
    {
        using var fixture = new ProvisioningFixture();
        var source = new MutableValueSource { Failure = new InvalidOperationException("Not provisioned yet") };
        var output = fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        var input = fixture.Bitwarden.AddSecret("input");
        fixture.AddRemoteSecret("output", "old-output");
        fixture.AddRemoteSecret("input", "existing-input");
        var state = new FakeDeploymentStateManager();
        fixture.Builder.Services.AddSingleton<IDeploymentStateManager>(state);
        using var app = fixture.Builder.Build();

        await fixture.Provisioner.PreSyncManagedSecretValuesAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);

        Assert.Equal("existing-input", state.GetSavedValue($"Parameters:{input.Resource.Name}"));
        Assert.DoesNotContain($"Parameters:{output.Resource.Name}", state.SavedSectionNames);
        Assert.Equal(0, source.Calls);
        Assert.Null(fixture.Bitwarden.Resource.ResolveSecretValue(output.Resource));
    }

    [Fact]
    public async Task MixedManagedSecrets_SyncsOnlyMissingParameterInputs()
    {
        using var fixture = new ProvisioningFixture();
        var source = new MutableValueSource { Failure = new InvalidOperationException("Not provisioned yet") };
        var output = fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        var input = fixture.Bitwarden.AddSecret("input");
        fixture.AddRemoteSecret("output", "old-output");
        fixture.AddRemoteSecret("input", "existing-input");
        using var app = fixture.Builder.Build();
        await fixture.InitializeAsync(app.Services);

        await fixture.Provisioner.SyncMissingManagedSecretValuesAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);

        Assert.Equal("existing-input", await ((IValueProvider)input.Resource).GetValueAsync(default));
        Assert.Null(fixture.Bitwarden.Resource.ResolveSecretValue(output.Resource));
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Provisioning_UsesFreshSourceEachTime_PreservingRemoteIdentity()
    {
        using var fixture = new ProvisioningFixture();
        var source = new MutableValueSource { Value = "first-output" };
        var secret = fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"), "Reporting DSN");
        var existing = fixture.AddRemoteSecret("Reporting DSN", "previous-deployment");
        using var app = fixture.Builder.Build();
        await fixture.InitializeAsync(app.Services);

        await fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);
        Assert.Equal("first-output", fixture.Provider.Secrets[existing].Value);
        source.Value = "second-output";
        await fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);

        Assert.Equal("second-output", fixture.Provider.Secrets[existing].Value);
        Assert.Equal("Reporting DSN", fixture.Provider.Secrets[existing].Key);
        Assert.Equal(existing, secret.Resource.SecretId);
        Assert.Equal(2, fixture.Provider.UpdatedSecrets.Count);
        Assert.Empty(fixture.Provider.CreatedSecrets);
    }

    [Fact]
    public async Task FailedSource_DoesNotWriteOrFallBackToPreviousOutput()
    {
        using var fixture = new ProvisioningFixture();
        var source = new MutableValueSource { Value = "first-output" };
        var secret = fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        using var app = fixture.Builder.Build();
        await fixture.InitializeAsync(app.Services);
        await fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);
        source.Failure = new InvalidOperationException("Upstream provisioning failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Provisioner.ProvisionSecretsAsync(
            fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default));

        Assert.Equal("first-output", fixture.Provider.Secrets[secret.Resource.SecretId!.Value].Value);
        Assert.Single(fixture.Provider.CreatedSecrets);
        Assert.Empty(fixture.Provider.UpdatedSecrets);
    }

    [Fact]
    public async Task UnresolvedSource_WaitsForItsOwner_AndCancellationCannotUseStoredValue()
    {
        using var fixture = new ProvisioningFixture();
        var source = new PendingValueSource();
        fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        var existing = fixture.AddRemoteSecret("output", "previous-deployment");
        using var app = fixture.Builder.Build();
        await fixture.InitializeAsync(app.Services);
        using var cancellation = new CancellationTokenSource();
        var provisioning = fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, cancellation.Token);
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(provisioning.IsCompleted);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provisioning);

        Assert.Equal("previous-deployment", fixture.Provider.Secrets[existing].Value);
        Assert.Empty(fixture.Provider.CreatedSecrets);
        Assert.Empty(fixture.Provider.UpdatedSecrets);
    }

    [Fact]
    public async Task ReferenceParameter_OverrideCanBeRemovedWithoutReusingPreviousOutput()
    {
        using var fixture = new ProvisioningFixture();
        var source = new MutableValueSource { Value = "generated" };
        var secret = fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        fixture.Builder.Configuration["Parameters:bitwarden-output"] = "override";
        using var app = fixture.Builder.Build();
        await fixture.InitializeAsync(app.Services);
        await fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);
        Assert.Equal(0, source.Calls);
        Assert.Equal("override", await secret.Resource.GetValueAsync(default));
        Assert.Equal("override", fixture.Provider.Secrets[secret.Resource.SecretId!.Value].Value);

        fixture.Builder.Configuration["Parameters:bitwarden-output"] = null;
        await fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);
        Assert.Equal(1, source.Calls);
        Assert.Equal("generated", await secret.Resource.GetValueAsync(default));
        Assert.Equal("generated", await ((IValueProvider)secret.Resource).GetValueAsync(default));
        Assert.Equal("generated", fixture.Provider.Secrets[secret.Resource.SecretId!.Value].Value);
    }

    [Fact]
    public async Task RealParameterProcessing_DoesNotPersistGeneratedValuesAcrossOperations()
    {
        using var fixture = new ProvisioningFixture(publish: true);
        var source = new MutableValueSource { Value = "first" };
        var secret = fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        var state = new FakeDeploymentStateManager();
        fixture.Builder.Services.AddSingleton<IDeploymentStateManager>(state);
        fixture.Builder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: false, isAvailable: false));
        using var app = fixture.Builder.Build();
        var processor = app.Services.GetRequiredService<ParameterProcessor>();
        var model = new DistributedApplicationModel(fixture.Builder.Resources);
        for (int operation = 0; operation < 2; operation++)
        {
            await fixture.Provisioner.PreSyncManagedSecretValuesAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);
            await processor.InitializeParametersAsync(model, waitForResolution: true);
            Assert.True(string.IsNullOrEmpty(await secret.Resource.GetValueAsync(default))); // Aspire 13.5 normalizes absent inputs to empty.
            await fixture.InitializeAsync(app.Services);
            source.Value = $"value-{operation}";
            await fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);
            Assert.Equal(source.Value, await secret.Resource.GetValueAsync(default));
            Assert.DoesNotContain("Parameters:bitwarden-output", state.SavedSectionNames);
        }
        Assert.Equal(2, source.Calls);
        Assert.Single(fixture.Provider.CreatedSecrets);
        Assert.Single(fixture.Provider.UpdatedSecrets);
    }

    [Fact]
    public async Task LatePrefill_ReplacesFaultedLazyInputBeforeAnotherParameterPass()
    {
        using var fixture = new ProvisioningFixture(publish: true);
        var secret = fixture.Bitwarden.AddSecret("input");
        fixture.AddRemoteSecret("input", "remote-input");
        var state = new FakeDeploymentStateManager();
        fixture.Builder.Services.AddSingleton<IDeploymentStateManager>(state);
        fixture.Builder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: false, isAvailable: false));
        using var app = fixture.Builder.Build();
        var processor = app.Services.GetRequiredService<ParameterProcessor>();
        var model = new DistributedApplicationModel(fixture.Builder.Resources);
        await processor.InitializeParametersAsync(model);
        await Assert.ThrowsAnyAsync<Exception>(() => secret.Resource.GetValueAsync(default).AsTask());
        await fixture.InitializeAsync(app.Services);
        await fixture.Provisioner.SyncMissingManagedSecretValuesAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default);
        await processor.InitializeParametersAsync(model);
        Assert.Equal("remote-input", await secret.Resource.GetValueAsync(default));
        Assert.Equal("remote-input", await ((IValueProvider)secret.Resource).GetValueAsync(default));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task EmptyReferenceOutput_FaultsBothGettersAndNeverWrites(string? value)
    {
        using var fixture = new ProvisioningFixture();
        var source = new MutableValueSource { Value = value! };
        var secret = fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        using var app = fixture.Builder.Build();
        await fixture.InitializeAsync(app.Services);
        await Assert.ThrowsAsync<DistributedApplicationException>(() => fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, app.Services, NullLogger.Instance, default));
        await Assert.ThrowsAsync<DistributedApplicationException>(() => secret.Resource.GetValueAsync(default).AsTask());
        await Assert.ThrowsAsync<DistributedApplicationException>(() => ((IValueProvider)secret.Resource).GetValueAsync(default).AsTask());
        Assert.Empty(fixture.Provider.CreatedSecrets);
    }
    [Fact]
    public async Task Publish_PreservesInputsWithoutContactingBitwardenOrComputedSources()
    {
        var builder = DistributedApplication.CreateBuilder();
        var manager = AddManager(builder);
        manager.AddSecret("input");
        var source = new MutableValueSource { Failure = new InvalidOperationException("Must remain deferred") };
        var reference = manager.AddSecret("output", ReferenceExpression.Create($"{source}"));
        reference.Resource.SetParameterValue("previous-generated");
        using var services = new ServiceCollection().Configure<PipelineOptions>(options => options.Step = WellKnownPipelineSteps.Publish).BuildServiceProvider();
        var provisioner = new BitwardenSecretManagerProvisioner(new UnexpectedProviderFactory());
        await provisioner.PreSyncManagedSecretValuesAsync(manager.Resource, services, NullLogger.Instance, default);
        Assert.Null(await reference.Resource.GetValueAsync(default));
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task DeployPipeline_WaitsForProducerThenWritesItsFreshParameterValue()
    {
        using var fixture = new ProvisioningFixture(publish: true);
        var source = new MutableValueSource();
        var secret = fixture.Bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = new FakeDeploymentStateManager();
        fixture.Builder.Services.AddSingleton<IDeploymentStateManager>(state);
        fixture.Builder.Services.Configure<PipelineOptions>(options => options.Step = "test-write");
        fixture.Builder.Pipeline.AddStep("test-producer", async context =>
        {
            started.SetResult();
            await release.Task.WaitAsync(context.CancellationToken);
            source.Value = "pipeline-produced";
        }, dependsOn: WellKnownPipelineSteps.ProcessParameters);
        fixture.Builder.Pipeline.AddStep("test-write", async context =>
        {
            await fixture.InitializeAsync(context.Services);
            await fixture.Provisioner.ProvisionSecretsAsync(fixture.Bitwarden.Resource, context.Services, NullLogger.Instance, context.CancellationToken);
        }, dependsOn: "test-producer");
        using var app = fixture.Builder.Build();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var context = new PipelineContext(new DistributedApplicationModel(fixture.Builder.Resources), fixture.Builder.ExecutionContext, app.Services, NullLogger.Instance, deadline.Token);
        var execution = fixture.Builder.Pipeline.ExecuteAsync(context);
        try
        {
            await started.Task.WaitAsync(deadline.Token);
            Assert.Equal(0, source.Calls);
            Assert.Empty(fixture.Provider.CreatedSecrets);
        }
        finally
        {
            release.TrySetResult();
        }
        await execution.WaitAsync(deadline.Token);
        Assert.Equal("pipeline-produced", await secret.Resource.GetValueAsync(default));
        Assert.Equal("pipeline-produced", fixture.Provider.Secrets[secret.Resource.SecretId!.Value].Value);
        Assert.DoesNotContain("Parameters:bitwarden-output", state.SavedSectionNames);
    }
    private static IResourceBuilder<BitwardenSecretManagerResource> AddManager(IDistributedApplicationBuilder builder) =>
        builder.AddBitwardenSecretManager("bitwarden", builder.AddParameter("project"),
            builder.AddParameter("organization"), builder.AddParameter("token", secret: true));

    private sealed class MutableValueSource : IValueProvider, IManifestExpressionProvider
    {
        internal string Value { get; set; } = string.Empty;
        internal Exception? Failure { get; set; }
        internal int Calls { get; private set; }
        internal IResource? Caller { get; private set; }
        public string ValueExpression => "{generated.output}";

        public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Failure is null ? ValueTask.FromResult<string?>(Value) : ValueTask.FromException<string?>(Failure);
        }

        public ValueTask<string?> GetValueAsync(ValueProviderContext context, CancellationToken cancellationToken = default)
        {
            Caller = context.Caller;
            return GetValueAsync(cancellationToken);
        }
    }

    private sealed class PendingValueSource : IValueProvider, IManifestExpressionProvider
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ValueExpression => "{generated.output}";

        public async ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class UnexpectedProviderFactory : IBitwardenSecretManagerProviderFactory
    {
        public IBitwardenSecretManagerProvider Create(string apiUrl, string identityUrl) =>
            throw new InvalidOperationException("Pre-sync must not contact Bitwarden for explicit outputs.");
    }

    private sealed class ProvisioningFixture : IDisposable
    {
        private readonly string directory = Directory.CreateTempSubdirectory("bitwarden-output-tests-").FullName;
        private readonly Guid organizationId = Guid.NewGuid();
        private readonly Guid projectId = Guid.NewGuid();
        internal IDistributedApplicationBuilder Builder { get; }
        internal IResourceBuilder<BitwardenSecretManagerResource> Bitwarden { get; }
        internal FakeBitwardenProvider Provider { get; } = new();
        internal BitwardenSecretManagerProvisioner Provisioner { get; }

        internal ProvisioningFixture(bool publish = false)
        {
            Builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = publish ? ["Publishing:Publisher=manifest", "Publishing:OutputPath=./unused"] : [],
                DisableDashboard = true
            });
            Builder.Configuration["Parameters:project"] = projectId.ToString("D");
            Builder.Configuration["Parameters:organization"] = organizationId.ToString("D");
            Builder.Configuration["Parameters:token"] = "0.ec2c1d46-6a4b-4751-a310-af9601317f2d.fake-secret:AAAAAAAAAAAAAAAAAAAAAA==";
            Builder.Configuration["Aspire:Store:Path"] = directory;
            Bitwarden = AddManager(Builder).WithCacheFile(Path.Combine(directory, "state.json")).WithAuthCacheDirectory(directory);
            Provider.Projects[projectId] = new(projectId, "shared-project", organizationId);
            var factory = new FakeBitwardenProviderFactory(Provider);
            Builder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(factory);
            Provisioner = new(factory);
        }

        internal Guid AddRemoteSecret(string name, string value)
        {
            var id = Guid.NewGuid();
            Provider.Secrets[id] = new(id, name, value, string.Empty, organizationId, projectId);
            return id;
        }

        internal async Task InitializeAsync(IServiceProvider services)
        {
            await Provisioner.AuthenticateAsync(Bitwarden.Resource, services, NullLogger.Instance, default);
            await Provisioner.ProvisionProjectAsync(Bitwarden.Resource, services, NullLogger.Instance, default);
        }

        public void Dispose() => Directory.Delete(directory, recursive: true);
    }
}