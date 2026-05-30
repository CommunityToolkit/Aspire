using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretManagerProvisionerTests
{
    [Fact]
    public async Task ProvisionAsync_CreatesProjectAndManagedSecret()
    {
        var organizationId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");
        var authStateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.auth.bin");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "managed-secret-value";

            var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "team-secrets", organizationParameter, accessToken)
                .WithCacheFile(stateFile)
                .WithAuthCacheFile(authStateFile);
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
            Assert.Equal(authStateFile, fakeProvider.AuthCacheFile);
        }
        finally
        {
            if (File.Exists(stateFile))
            {
                File.Delete(stateFile);
            }

            if (File.Exists(authStateFile))
            {
                File.Delete(authStateFile);
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
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
            appBuilder.Configuration["Parameters:bitwarden-project-name"] = "shared-team-secrets";

            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var projectName = appBuilder.AddParameter("bitwarden-project-name");

            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", projectName, organizationId, accessToken)
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
    public async Task ProvisionAsync_UsesExistingProjectWithoutRenaming()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "different-name", organizationId, accessToken)
                .WithExistingProject(existingProjectId)
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
    public async Task ProvisionAsync_AdoptsExplicitExistingSecret()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var existingSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "updated-value";

            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "application-secrets", organizationId, accessToken)
                .WithExistingProject(existingProjectId)
                .WithCacheFile(stateFile);

            var managedSecret = bitwarden.AddSecret("managed-secret")
                .WithExistingSecret(existingSecretId);

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "existing-project-name", organizationId);
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "managed-secret", "stale-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Equal(existingSecretId, managedSecret.Resource.SecretId);
            Assert.Contains(existingSecretId, fakeProvider.UpdatedSecrets);
            Assert.Equal("updated-value", bitwarden.Resource.ResolveSecretValue(managedSecret.Resource));
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
    public async Task ProvisionAsync_AdoptsExplicitExistingSecret_DoesNotUpdateWhenUnchanged()
    {
        var organizationId = Guid.NewGuid();
        var existingProjectId = Guid.NewGuid();
        var existingSecretId = Guid.NewGuid();
        var stateFile = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.json");

        try
        {
            var appBuilder = DistributedApplication.CreateBuilder();
            appBuilder.Configuration["Aspire:Store:Path"] = Path.GetTempPath();
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "unchanged-value";

            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "application-secrets", organizationId, accessToken)
                .WithExistingProject(existingProjectId)
                .WithCacheFile(stateFile);

            var managedSecret = bitwarden.AddSecret("managed-secret")
                .WithExistingSecret(existingSecretId);

            var fakeProvider = new FakeBitwardenProvider();
            fakeProvider.Projects[existingProjectId] = new BitwardenProjectInfo(existingProjectId, "existing-project-name", organizationId);
            fakeProvider.Secrets[existingSecretId] = new BitwardenSecretInfo(existingSecretId, "managed-secret", "unchanged-value", string.Empty, organizationId, existingProjectId);
            appBuilder.Services.AddSingleton<IBitwardenSecretManagerProviderFactory>(new FakeBitwardenProviderFactory(fakeProvider));

            using var app = appBuilder.Build();
            var provisioner = app.Services.GetRequiredService<BitwardenSecretManagerProvisioner>();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<BitwardenSecretManagerProvisioner>();

            await provisioner.AuthenticateAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionProjectAsync(bitwarden.Resource, app.Services, logger, default);
            await provisioner.ProvisionSecretsAsync(bitwarden.Resource, app.Services, logger, default);

            Assert.Equal(existingSecretId, managedSecret.Resource.SecretId);
            Assert.DoesNotContain(existingSecretId, fakeProvider.UpdatedSecrets);
            Assert.Equal("unchanged-value", bitwarden.Resource.ResolveSecretValue(managedSecret.Resource));
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
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "managed-secret-value";

            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "application-secrets", organizationId, accessToken)
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
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "application-secrets", organizationId, accessToken)
                .WithExistingProject(existingProjectId)
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
            appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
            appBuilder.Configuration["Parameters:bitwarden-managed-secret"] = "configured-value";

            var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
            var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "application-secrets", organizationId, accessToken)
                .WithExistingProject(existingProjectId)
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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
