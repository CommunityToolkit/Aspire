#pragma warning disable ASPIREPIPELINES002

using Aspire.Hosting;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

/// <summary>
/// Tests for <see cref="BitwardenSecretManagerProvisioner.PreSyncManagedSecretValuesAsync"/>.
/// Covers the credential-missing early-exit paths, the config-already-present skip, and the
/// happy-path upstream fetch + deployment-state save.
/// </summary>
public class BitwardenSecretManagerProvisionerPreSyncTests
{
    private const string FakeAccessToken = "0.ec2c1d46-6a4b-4751-a310-af9601317f2d.fake-secret:AAAAAAAAAAAAAAAAAAAAAA==";

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_NoManagedSecrets_ReturnsBeforeAccessingDeploymentState()
    {
        // IDeploymentStateManager is intentionally NOT registered here.
        // The method must return before calling GetRequiredService<IDeploymentStateManager>()
        // when there are no managed secrets.
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = Guid.NewGuid().ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
        appBuilder.Configuration["Parameters:bitwarden-project"] = "my-project";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var projectParam = appBuilder.AddParameter("bitwarden-project");

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken);
        bitwarden.GetSecret("external-key"); // unmanaged only — no managed secrets

        var fakeProvider = new FakeBitwardenProvider();
        appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

        using var app = appBuilder.Build();
        var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

        // Should not throw InvalidOperationException for missing IDeploymentStateManager.
        await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

        Assert.Empty(bitwarden.Resource.ManagedSecrets);
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_TokenMissing_NoInteraction_ReturnsWithoutSaving()
    {
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            // Access token deliberately absent from config.
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = Guid.NewGuid().ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-project"] = "my-project";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

            // Aspire 13 registers IInteractionService with IsAvailable=true by default.
            // Override to simulate a non-interactive environment.
#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: false, isAvailable: false));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Empty(fakeDeploymentState.SavedSectionNames);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_TokenMissing_InteractionCanceled_ReturnsWithoutSaving()
    {
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            // Access token absent — interaction will be prompted but then canceled.
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = Guid.NewGuid().ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-project"] = "my-project";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: true));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Empty(fakeDeploymentState.SavedSectionNames);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_OrgMissing_NoInteraction_ReturnsWithoutSaving()
    {
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            // Organization ID deliberately absent — no IInteractionService to prompt for it.
            appBuilder.Configuration["Parameters:bitwarden-project"] = "my-project";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

            // Aspire 13 registers IInteractionService with IsAvailable=true by default.
            // Override to simulate a non-interactive environment.
#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: false, isAvailable: false));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Empty(fakeDeploymentState.SavedSectionNames);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_OrgPresentButInvalidGuid_ReturnsWithoutSaving()
    {
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = "not-a-guid";
            appBuilder.Configuration["Parameters:bitwarden-project"] = "my-project";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Empty(fakeDeploymentState.SavedSectionNames);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_SecretAlreadyInConfig_SkipsSave()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var existingSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-project"] = existingProjectId.ToString("D");
            // Secret value already present in config — pre-sync must skip the Bitwarden fetch for it.
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "already-configured-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "managed-secret", "upstream-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            // The secret was already in config so no deployment state save should have occurred.
            Assert.DoesNotContain("Parameters:bitwarden-managed-secret", fakeDeploymentState.SavedSectionNames);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_ProjectMissing_NoInteraction_ReturnsWithoutSaving()
    {
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = Guid.NewGuid().ToString("D");
            // Project deliberately absent — no IInteractionService to prompt for it.

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: false, isAvailable: false));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Empty(fakeDeploymentState.SavedSectionNames);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_ProjectMissing_InteractionPrompts_SavesProjectAndSecret()
    {
        // Project is absent from config but the user is prompted and enters the project ID as a GUID.
        // Pre-sync should save both the project ID and the managed secret value.
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var existingSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            // Project deliberately absent — will be prompted.

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[projectId] = new BitwardenProjectInfo(projectId, "my-project", organizationId);
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "managed-secret", "upstream-value", string.Empty, organizationId, projectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

            // Interaction returns the project ID as a GUID.
#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(projectId.ToString("D")));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Contains("Parameters:bitwarden-project", fakeDeploymentState.SavedSectionNames);
            Assert.Equal(projectId.ToString("D"), fakeDeploymentState.GetSavedValue("Parameters:bitwarden-project"));
            Assert.Contains("Parameters:bitwarden-managed-secret", fakeDeploymentState.SavedSectionNames);
            Assert.Equal("upstream-value", fakeDeploymentState.GetSavedValue("Parameters:bitwarden-managed-secret"));
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_ProjectNameInConfig_NoGuid_SkipsManagedSecretFetch()
    {
        // Project is specified as a name (not a GUID) and there is no cached project ID.
        // Pre-sync must NOT search org-wide and must NOT save any secret value, even when a
        // matching secret exists somewhere in the org.
        var organizationId = Guid.NewGuid();
        var unrelatedProjectId = Guid.NewGuid();
        var existingSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            // Project supplied as a NAME — not a GUID, so projectId stays null (no cache either).
            appBuilder.Configuration["Parameters:bitwarden-project"] = "my-project";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            // Secret exists in the org under a different project — pre-sync must not pick it up.
            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "managed-secret", "upstream-value", string.Empty, organizationId, unrelatedProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Empty(fakeDeploymentState.SavedSectionNames);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_StaleCachedProjectId_SkipsSecretSave()
    {
        // When the cached project ID no longer exists in Bitwarden, pre-sync should warn and
        // skip secret saves — no secrets can be fetched for a deleted project.
        var organizationId = Guid.NewGuid();
        var staleProjectId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(stateFile, $$"""
                {
                  "projectId": "{{staleProjectId:D}}",
                  "managedSecretIds": {},
                  "nameBindings": {}
                }
                """);

            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            // No project in config — project ID loaded from cache (which is stale).

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            // staleProjectId deliberately NOT registered — simulates project deleted from Bitwarden.
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.DoesNotContain("Parameters:bitwarden-managed-secret", fakeDeploymentState.SavedSectionNames);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task PreSyncManagedSecretValuesAsync_SecretNotInConfig_UpstreamFound_SavesValue()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var existingSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            // Project supplied as a GUID so pre-sync can resolve the project ID without a cache.
            appBuilder.Configuration["Parameters:bitwarden-project"] = existingProjectId.ToString("D");
            // Secret value deliberately absent — pre-sync should fetch it from Bitwarden.

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "managed-secret", "upstream-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            var fakeDeploymentState = new FakeDeploymentStateManager();
            appBuilder.Services.AddSingleton<IDeploymentStateManager>(fakeDeploymentState);

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.PreSyncManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            const string expectedKey = "Parameters:bitwarden-managed-secret";
            Assert.Contains(expectedKey, fakeDeploymentState.SavedSectionNames);
            Assert.Equal("upstream-value", fakeDeploymentState.GetSavedValue(expectedKey));
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }
}
