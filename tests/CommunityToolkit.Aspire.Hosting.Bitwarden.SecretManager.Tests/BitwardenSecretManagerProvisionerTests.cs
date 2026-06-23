using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretManagerProvisionerTests
{
    // A structurally valid Bitwarden access token. Format: 0.<uuid>.<secret>:<base64_key>
    // The UUID component becomes the auth cache filename within the configured directory.
    private const string FakeAccessToken = "0.ec2c1d46-6a4b-4751-a310-af9601317f2d.fake-secret:AAAAAAAAAAAAAAAAAAAAAA==";
    private const string FakeAccessTokenId = "ec2c1d46-6a4b-4751-a310-af9601317f2d";

    [Fact]
    public async Task ProvisionAsync_CreatesProjectAndManagedSecret()
    {
        var organizationId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");
        var authStateDir = Path.Combine(Path.GetTempPath(), $"bitwarden-auth-{Guid.NewGuid():N}");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = "team-secrets";
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "managed-secret-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile)
                .WithAuthCacheDirectory(authStateDir);
            var managedSecret = bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.NotEqual(Guid.Empty, bitwarden.Resource.ProjectId!.Value);
            Assert.Single(fakeProvider.CreatedProjects);
            Assert.Single(fakeProvider.CreatedSecrets);
            Assert.NotNull(managedSecret.Resource.SecretId);
            Assert.Equal("managed-secret-value", bitwarden.Resource.ResolveSecretValue(managedSecret.Resource));
            Assert.True(File.Exists(bitwarden.Resource.CacheFile));
            Assert.Equal(Path.Combine(authStateDir, FakeAccessTokenId), fakeProvider.AuthCacheFile);
        }
        finally
        {
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }
        }
    }

    [Fact]
    public async Task ProvisionAsync_UsesParameterBackedProjectName()
    {
        var organizationId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project-name"] = "shared-team-secrets";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectName = appBuilder.AddParameter("bitwarden-project-name");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectName, organizationParameter, accessToken)
                .WithCacheFile(stateFile);

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Single(fakeProvider.CreatedProjects);
            Assert.Equal("shared-team-secrets", fakeProvider.Projects[fakeProvider.CreatedProjects[0]].Name);
            Assert.NotNull(fakeProvider.AuthCacheFile);
        }
        finally
        {
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }
        }
    }

    [Fact]
    public async Task ProvisionAsync_WhenProjectNameOrIdIsGuid_AdoptsExistingProjectById()
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

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "existing-remote-name", organizationId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Equal(existingProjectId, bitwarden.Resource.ProjectId);
            Assert.Empty(fakeProvider.CreatedProjects);
            Assert.Empty(fakeProvider.UpdatedProjects);
        }
        finally
        {
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }
        }
    }

    [Fact]
    public async Task ProvisionAsync_WhenManagedSecretIsAlsoReferencedByName_TreatsItAsSingleSecret()
    {
        var organizationId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
            appBuilder.Configuration["Parameters:bitwarden-project"] = "application-secrets";
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "managed-secret-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);

            var managedSecret = bitwarden.AddSecret("managed-secret", "shared-secret");
            var reference = bitwarden.GetSecret("shared-secret");

            var fakeProvider = new FakeBitwardenProvider();
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            Assert.Same(managedSecret.Resource, reference.Resource);
            Assert.Single(bitwarden.Resource.DeclaredSecretReferences);

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.NotNull(managedSecret.Resource.SecretId);
            Assert.Single(fakeProvider.CreatedSecrets);
            Assert.Equal("managed-secret-value", bitwarden.Resource.ResolveSecretValue(managedSecret.Resource));
        }
        finally
        {
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }
        }
    }

    [Fact]
    public async Task SyncMissingManagedSecretValuesAsync_UsesExistingUpstreamValueWhenParameterIsMissing()
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

            var managedSecret = bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "application-secrets", organizationId);
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "managed-secret", "upstream-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.SyncMissingManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Equal(existingSecretId, managedSecret.Resource.SecretId);
            Assert.Empty(fakeProvider.CreatedSecrets);
            Assert.Empty(fakeProvider.UpdatedSecrets);
            Assert.Equal("upstream-value", bitwarden.Resource.ResolveSecretValue(managedSecret.Resource));
        }
        finally
        {
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }
        }
    }

    [Fact]
    public async Task SyncMissingManagedSecretValuesAsync_DoesNotOverrideConfiguredParameterValue()
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
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "configured-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);

            var managedSecret = bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "application-secrets", organizationId);
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "managed-secret", "upstream-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.SyncMissingManagedSecretValuesAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Equal(existingSecretId, managedSecret.Resource.SecretId);
            Assert.Contains(existingSecretId, fakeProvider.UpdatedSecrets);
            Assert.Equal("configured-value", bitwarden.Resource.ResolveSecretValue(managedSecret.Resource));
        }
        finally
        {
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }
        }
    }

    [Fact]
    public async Task ProvisionSecretsAsync_DuplicateManagedSecretNames_NonInteractive_Throws()
    {
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
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "new-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);

            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "application-secrets", organizationId);
            fakeProvider.Secrets[dup1Id] = new BitwardenSecretInfo(dup1Id, "managed-secret", "value-1", string.Empty, organizationId, existingProjectId);
            fakeProvider.Secrets[dup2Id] = new BitwardenSecretInfo(dup2Id, "managed-secret", "value-2", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            // Aspire 13 registers IInteractionService with IsAvailable=true by default.
            // Override it to simulate a non-interactive environment.
#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: false, isAvailable: false));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);

            var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
                () => provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default));

            Assert.Contains("bitwarden", ex.Message);
            Assert.Contains("managed-secret", ex.Message);
            Assert.Contains(dup1Id.ToString("D"), ex.Message);
            Assert.Contains(dup2Id.ToString("D"), ex.Message);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task ProvisionSecretsAsync_DuplicateManagedSecretNames_Interactive_UserPicksCandidate_SyncsSelected()
    {
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
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "new-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);

            var managedSecret = bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "application-secrets", organizationId);
            fakeProvider.Secrets[dup1Id] = new BitwardenSecretInfo(dup1Id, "managed-secret", "value-1", string.Empty, organizationId, existingProjectId);
            fakeProvider.Secrets[dup2Id] = new BitwardenSecretInfo(dup2Id, "managed-secret", "value-2", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(dup2Id.ToString("D")));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Equal(dup2Id, managedSecret.Resource.SecretId);
            Assert.Contains(dup2Id, fakeProvider.UpdatedSecrets);
            Assert.DoesNotContain(dup1Id, fakeProvider.UpdatedSecrets);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task ProvisionSecretsAsync_DuplicateManagedSecretNames_Interactive_UserCancels_Throws()
    {
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
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "new-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);

            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "application-secrets", organizationId);
            fakeProvider.Secrets[dup1Id] = new BitwardenSecretInfo(dup1Id, "managed-secret", "value-1", string.Empty, organizationId, existingProjectId);
            fakeProvider.Secrets[dup2Id] = new BitwardenSecretInfo(dup2Id, "managed-secret", "value-2", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: true));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);

            var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
                () => provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default));

            Assert.Contains("canceled", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task ProvisionSecretsAsync_DuplicateManagedSecretNames_Interactive_InvalidInput_Throws()
    {
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
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "new-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectParam = appBuilder.AddParameter("bitwarden-project");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken)
                .WithCacheFile(stateFile);

            bitwarden.AddSecret("managed-secret");

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "application-secrets", organizationId);
            fakeProvider.Secrets[dup1Id] = new BitwardenSecretInfo(dup1Id, "managed-secret", "value-1", string.Empty, organizationId, existingProjectId);
            fakeProvider.Secrets[dup2Id] = new BitwardenSecretInfo(dup2Id, "managed-secret", "value-2", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

#pragma warning disable ASPIREINTERACTION001
            appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService("not-a-guid"));
#pragma warning restore ASPIREINTERACTION001

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);

            var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
                () => provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default));

            Assert.Contains("not-a-guid", ex.Message);
        }
        finally
        {
            if (File.Exists(stateFile)) File.Delete(stateFile);
        }
    }

    [Fact]
    public async Task ProvisionProjectAsync_MissingProjectName_NonInteractive_ThrowsDescriptiveError()
    {
        // Credentials are present but the project name is not — simulates a state file
        // that was partially set up before the project was ever configured.
        var organizationId = Guid.NewGuid();

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = FakeAccessToken;
        // bitwarden-project intentionally absent from config

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var projectParam = appBuilder.AddParameter("bitwarden-project");

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken);

        var fakeProvider = new FakeBitwardenProvider();
        appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

#pragma warning disable ASPIREINTERACTION001
        appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: false, isAvailable: false));
#pragma warning restore ASPIREINTERACTION001

        using var app = appBuilder.Build();
        var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

        await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);

        var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
            () => provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default));

        Assert.Contains("bitwarden-project", ex.Message);
        Assert.Contains("non-interactive", ex.Message);
    }

    [Fact]
    public async Task AuthenticateAsync_MissingAccessToken_NonInteractive_ThrowsDescriptiveError()
    {
        // Access token is absent — simulates running --non-interactive before first interactive run.
        var organizationId = Guid.NewGuid();

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        // bitwarden-access-token intentionally absent from config

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var projectParam = appBuilder.AddParameter("bitwarden-project");

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectParam, organizationParameter, accessToken);

        var fakeProvider = new FakeBitwardenProvider();
        appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

#pragma warning disable ASPIREINTERACTION001
        appBuilder.Services.AddSingleton<IInteractionService>(new FakeInteractionService(canceled: false, isAvailable: false));
#pragma warning restore ASPIREINTERACTION001

        using var app = appBuilder.Build();
        var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

        var ex = await Assert.ThrowsAsync<DistributedApplicationException>(
            () => provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default));

        Assert.Contains("bitwarden-access-token", ex.Message);
        Assert.Contains("non-interactive", ex.Message);
    }
}

internal sealed class FakeBitwardenProviderFactory(FakeBitwardenProvider provider) : IBitwardenSecretManagerProviderFactory
{
    public IBitwardenSecretManagerProvider Create(string apiUrl, string identityUrl)
    {
        provider.ApiUrl = apiUrl;
        provider.IdentityUrl = identityUrl;
        return provider;
    }
}

internal sealed class FakeBitwardenProvider : IBitwardenSecretManagerProvider
{
    public Dictionary<Guid, BitwardenProjectInfo> Projects { get; } = [];

    public Dictionary<Guid, BitwardenSecretInfo> Secrets { get; } = [];

    public List<Guid> CreatedProjects { get; } = [];

    public List<Guid> UpdatedProjects { get; } = [];

    public List<Guid> CreatedSecrets { get; } = [];

    public List<Guid> UpdatedSecrets { get; } = [];

    public string? ApiUrl { get; set; }

    public string? IdentityUrl { get; set; }

    public string? AccessToken { get; private set; }

    public string? AuthCacheFile { get; private set; }

    public void Login(string accessToken, string? authCacheFile)
    {
        AccessToken = accessToken;
        AuthCacheFile = authCacheFile;
    }

    public BitwardenProjectInfo? GetProject(Guid projectId)
        => Projects.TryGetValue(projectId, out BitwardenProjectInfo? project) ? project : null;

    public BitwardenProjectInfo CreateProject(Guid organizationId, string projectName)
    {
        BitwardenProjectInfo project = new(Guid.NewGuid(), projectName, organizationId);
        Projects[project.Id] = project;
        CreatedProjects.Add(project.Id);
        return project;
    }

    public BitwardenProjectInfo UpdateProject(Guid organizationId, Guid projectId, string projectName)
    {
        BitwardenProjectInfo project = new(projectId, projectName, organizationId);
        Projects[projectId] = project;
        UpdatedProjects.Add(projectId);
        return project;
    }

    public BitwardenSecretInfo? GetSecret(Guid secretId)
        => Secrets.TryGetValue(secretId, out BitwardenSecretInfo? secret) ? secret : null;

    public IReadOnlyList<BitwardenSecretInfo> GetSecretsByIds(Guid[] secretIds)
        => secretIds.Where(Secrets.ContainsKey).Select(secretId => Secrets[secretId]).ToArray();

    public IReadOnlyList<BitwardenSecretIdentifierInfo> ListSecrets(Guid organizationId)
        => Secrets.Values
            .Where(secret => secret.OrganizationId == organizationId)
            .Select(secret => new BitwardenSecretIdentifierInfo(secret.Id, secret.Key, secret.OrganizationId))
            .ToArray();

    public BitwardenSecretInfo CreateSecret(Guid organizationId, string remoteName, string value, Guid[] projectIds, string note = "")
    {
        BitwardenSecretInfo secret = new(Guid.NewGuid(), remoteName, value, note, organizationId, projectIds[0]);
        Secrets[secret.Id] = secret;
        CreatedSecrets.Add(secret.Id);
        return secret;
    }

    public BitwardenSecretInfo UpdateSecret(Guid organizationId, Guid secretId, string remoteName, string value, string note, Guid[] projectIds)
    {
        BitwardenSecretInfo secret = new(secretId, remoteName, value, note, organizationId, projectIds[0]);
        Secrets[secret.Id] = secret;
        UpdatedSecrets.Add(secret.Id);
        return secret;
    }

    public IReadOnlyList<BitwardenSecretInfo> SyncSecrets(Guid organizationId)
        => Secrets.Values.Where(s => s.OrganizationId == organizationId).ToArray();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

#pragma warning disable ASPIREINTERACTION001
internal sealed class FakeInteractionService : IInteractionService
{
    private readonly string? _returnValue;
    private readonly bool _canceled;
    private readonly bool _isAvailable;

    public FakeInteractionService(string returnValue, bool isAvailable = true) { _returnValue = returnValue; _isAvailable = isAvailable; }
    public FakeInteractionService(bool canceled, bool isAvailable = true) { _canceled = canceled; _isAvailable = isAvailable; }

    public bool IsAvailable => _isAvailable;

    public Task<InteractionResult<InteractionInput>> PromptInputAsync(
        string title,
        string? message,
        InteractionInput input,
        InputsDialogInteractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_canceled)
        {
            return Task.FromResult(InteractionResult.Cancel(input));
        }

        input.Value = _returnValue;
        return Task.FromResult(InteractionResult.Ok(input));
    }

    public Task<InteractionResult<bool>> PromptConfirmationAsync(string title, string message, MessageBoxInteractionOptions? options = null, CancellationToken cancellationToken = default)
        => Task.FromException<InteractionResult<bool>>(new NotSupportedException());

    public Task<InteractionResult<bool>> PromptMessageBoxAsync(string title, string message, MessageBoxInteractionOptions? options = null, CancellationToken cancellationToken = default)
        => Task.FromException<InteractionResult<bool>>(new NotSupportedException());

    public Task<InteractionResult<InteractionInput>> PromptInputAsync(string title, string? message, string inputLabel, string placeHolder, InputsDialogInteractionOptions? options = null, CancellationToken cancellationToken = default)
        => Task.FromException<InteractionResult<InteractionInput>>(new NotSupportedException());

    public Task<InteractionResult<InteractionInputCollection>> PromptInputsAsync(string title, string? message, IReadOnlyList<InteractionInput> inputs, InputsDialogInteractionOptions? options = null, CancellationToken cancellationToken = default)
        => Task.FromException<InteractionResult<InteractionInputCollection>>(new NotSupportedException());

    public Task<InteractionResult<bool>> PromptNotificationAsync(string title, string message, NotificationInteractionOptions? options = null, CancellationToken cancellationToken = default)
        => Task.FromException<InteractionResult<bool>>(new NotSupportedException());
}
#pragma warning restore ASPIREINTERACTION001

