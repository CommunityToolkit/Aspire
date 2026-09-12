// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES002

using Aspire.Hosting;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Extensions;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretValueSourceTests
{
    [Fact]
    public async Task ExplicitSource_IsDeferredAndExcludedFromPublish_WithoutAParameterInput()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var bitwarden = AddManager(builder);
        var source = new MutableValueSource { Value = "fresh-output" };
        var expression = ReferenceExpression.Create($"{source}");
        builder.Configuration["Parameters:bitwarden-output"] = "previous-deployment";

        var secret = bitwarden.AddSecret("output", expression, remoteName: "Reporting DSN");

        Assert.Equal(0, source.Calls);
        Assert.Equal(string.Empty, await secret.Resource.GetValueAsync(default));
        Assert.Equal(0, source.Calls);
        Assert.Equal("bitwarden-output", secret.Resource.Name);
        Assert.Equal("Reporting DSN", secret.Resource.RemoteName);
        Assert.True(secret.Resource.Secret);
        Assert.True(secret.Resource.IsManaged);
        Assert.Contains(expression, ((IValueWithReferences)secret.Resource).References);
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, secret.Resource.Annotations);
        Assert.Equal("{bitwarden.secrets.Reporting DSN}", ((IManifestExpressionProvider)secret.Resource).ValueExpression);
    }

    [Fact]
    public async Task ExplicitSource_OverridesBoundAndParameterValues_AndPreservesConsumerContext()
    {
        var builder = DistributedApplication.CreateBuilder();
        var bitwarden = AddManager(builder);
        var source = new MutableValueSource { Value = "fresh-output" };
        var secret = bitwarden.AddSecret("output", ReferenceExpression.Create($"{source}"));
        secret.Resource.InitializeWaitForValue();
        secret.Resource.ResolveWaitForValue("old-parameter");
        bitwarden.Resource.BindResolvedSecret(Guid.NewGuid(), "output", "old-remote-value");
        var caller = new ContainerResource("consumer");

        var value = await ((IValueProvider)secret.Resource).GetValueAsync(new ValueProviderContext { Caller = caller }, default);

        Assert.Equal("fresh-output", value);
        Assert.Same(caller, source.Caller);
        source.Value = "next-output";
        Assert.Equal("next-output", await ((IValueProvider)secret.Resource).GetValueAsync(default));
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
        internal IDistributedApplicationBuilder Builder { get; } = DistributedApplication.CreateBuilder();
        internal IResourceBuilder<BitwardenSecretManagerResource> Bitwarden { get; }
        internal FakeBitwardenProvider Provider { get; } = new();
        internal BitwardenSecretManagerProvisioner Provisioner { get; }

        internal ProvisioningFixture()
        {
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