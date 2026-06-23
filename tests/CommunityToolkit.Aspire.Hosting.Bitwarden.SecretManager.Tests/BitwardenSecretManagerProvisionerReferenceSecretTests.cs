using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

/// <summary>
/// Tests for <see cref="BitwardenSecretManagerProvisioner.SyncReferenceSecretValuesAsync"/>.
/// Covers the unmanaged (GetSecret) secret paths: by name and by ID.
/// </summary>
public class BitwardenSecretManagerProvisionerReferenceSecretTests
{
    private const string FakeAccessToken = "0.ec2c1d46-6a4b-4751-a310-af9601317f2d.fake-secret:AAAAAAAAAAAAAAAAAAAAAA==";

    [Fact]
    public async Task SyncReferenceSecretsAsync_NoUnmanagedSecrets_CompletesWithoutQueryingProvider()
    {
        // The early-return path: when only managed secrets are declared, the method returns
        // before even checking resource.ProjectId, so ProjectId can be null here.
        var organizationId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = "my-project";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            // ProjectId deliberately not set — method must exit before checking it.
            await provisioner.SyncReferenceSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Empty(bitwarden.Resource.UnmanagedSecrets);
            Assert.Null(bitwarden.Resource.ProjectId);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task SyncReferenceSecretsAsync_ByName_SingleMatch_SyncsValue()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var existingSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = existingProjectId.ToString("D");

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            var reference = bitwarden.GetSecret("api-key");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "app-project", organizationId);
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "api-key", "secret-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.SyncReferenceSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Equal(existingSecretId, reference.Resource.SecretId);
            Assert.Equal("secret-value", bitwarden.Resource.ResolveSecretValue(reference.Resource));
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task SyncReferenceSecretsAsync_ByName_NotFound_Throws()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = existingProjectId.ToString("D");

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.GetSecret("missing-key");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "app-project", organizationId);
            // No matching secret in provider.
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);

            var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
                () => provisioner.SyncReferenceSecretValuesAsync(bitwarden.Resource, app.Services, logger, default));

            Assert.Contains("missing-key", ex.Message);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task SyncReferenceSecretsAsync_ByName_DuplicateNames_Throws()
    {
        // Unlike managed secrets, reference secrets never offer interactive resolution for duplicates.
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var dup1Id = Guid.NewGuid();
        var dup2Id = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = existingProjectId.ToString("D");

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.GetSecret("api-key");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "app-project", organizationId);
            fakeProvider.Secrets[dup1Id] = new BitwardenSecretInfo(dup1Id, "api-key", "value-1", string.Empty, organizationId, existingProjectId);
            fakeProvider.Secrets[dup2Id] = new BitwardenSecretInfo(dup2Id, "api-key", "value-2", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);

            var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
                () => provisioner.SyncReferenceSecretValuesAsync(bitwarden.Resource, app.Services, logger, default));

            Assert.Contains("api-key", ex.Message);
            Assert.Contains("2", ex.Message);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task SyncReferenceSecretsAsync_ById_Found_SyncsValue()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var existingSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = existingProjectId.ToString("D");

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            var reference = bitwarden.GetSecret("api-key", existingSecretId);

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "app-project", organizationId);
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "api-key", "secret-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.SyncReferenceSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Equal(existingSecretId, reference.Resource.SecretId);
            Assert.Equal("secret-value", bitwarden.Resource.ResolveSecretValue(reference.Resource));
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task SyncReferenceSecretsAsync_ById_NotFound_Throws()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var unknownSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = existingProjectId.ToString("D");

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.GetSecret("api-key", unknownSecretId);

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "app-project", organizationId);
            // Secret with unknownSecretId not registered in provider.
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);

            var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
                () => provisioner.SyncReferenceSecretValuesAsync(bitwarden.Resource, app.Services, logger, default));

            Assert.Contains(unknownSecretId.ToString("D"), ex.Message);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task SyncReferenceSecretsAsync_ById_WrongProject_Throws()
    {
        var organizationId = Guid.NewGuid();
        var correctProjectId = Guid.NewGuid();
        var wrongProjectId = Guid.NewGuid();
        var secretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = correctProjectId.ToString("D");

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            bitwarden.GetSecret("api-key", secretId);

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[correctProjectId] = new BitwardenProjectInfo(correctProjectId, "app-project", organizationId);
            // Secret belongs to wrongProjectId, not correctProjectId.
            fakeProvider.Secrets[secretId] = new BitwardenSecretInfo(secretId, "api-key", "secret-value", string.Empty, organizationId, wrongProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);

            var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
                () => provisioner.SyncReferenceSecretValuesAsync(bitwarden.Resource, app.Services, logger, default));

            Assert.Contains(secretId.ToString("D"), ex.Message);
            Assert.Contains(correctProjectId.ToString("D"), ex.Message);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task FullPipeline_ManagedAndUnmanagedSecrets_BothResolved()
    {
        // Integration test: a resource with one managed secret and one unmanaged secret
        // goes through the complete provisioning pipeline and both end up with bound values.
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var unmanagedSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = existingProjectId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "managed-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);
            var managedSecret = bitwarden.AddSecret("managed-secret");
            var unmanagedRef = bitwarden.GetSecret("external-key");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "app-project", organizationId);
            fakeProvider.Secrets[unmanagedSecretId] = new BitwardenSecretInfo(unmanagedSecretId, "external-key", "external-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.SyncMissingManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.SyncReferenceSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            // Managed secret: created in Bitwarden, value bound.
            Assert.NotNull(managedSecret.Resource.SecretId);
            Assert.Equal("managed-value", bitwarden.Resource.ResolveSecretValue(managedSecret.Resource));

            // Unmanaged secret: fetched from existing Bitwarden secret, value bound.
            Assert.Equal(unmanagedSecretId, unmanagedRef.Resource.SecretId);
            Assert.Equal("external-value", bitwarden.Resource.ResolveSecretValue(unmanagedRef.Resource));
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }
}
