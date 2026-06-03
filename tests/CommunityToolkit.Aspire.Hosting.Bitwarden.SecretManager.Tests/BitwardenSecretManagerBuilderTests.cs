using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretManagerBuilderTests
{
    [Fact]
    public void AddBitwardenSecretManager_ParameterProjectName_WhenNull_Throws()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        IResourceBuilder<ParameterResource> projectName = null!;

        Action action = () => appBuilder.AddBitwardenSecretManager("bitwarden", projectName, Guid.NewGuid(), accessToken);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("projectName", exception.ParamName);
    }

    [Fact]
    public void AddSecret_WhenBuilderIsNull_Throws()
    {
        IResourceBuilder<BitwardenSecretManagerResource> builder = null!;

        Action action = () => BitwardenSecretManagerExtensions.AddSecret(builder, "managed-secret");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public async Task AddBitwardenSecretManager_StoresConfiguredProjectName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var organizationId = Guid.NewGuid();
        const string projectName = "app-secrets";

        appBuilder.AddBitwardenSecretManager("bitwarden", projectName, organizationId, accessToken);

        using var app = appBuilder.Build();

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());

        Assert.Equal("bitwarden", resource.Name);
        Assert.Equal(projectName, resource.RemoteProjectName);
        Assert.NotEqual(resource.Name, resource.RemoteProjectName);
        Assert.Equal(BitwardenSecretManagerResource.DefaultApiUrl, await resource.GetApiUrlAsync(CancellationToken.None));
        Assert.Equal(BitwardenSecretManagerResource.DefaultIdentityUrl, await resource.GetIdentityUrlAsync(CancellationToken.None));
        Assert.Null(resource.ProjectId);
    }

    [Fact]
    public void AddBitwardenSecretManager_ParameterProjectName_StoresParameterReference()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        appBuilder.Configuration["Parameters:bitwarden-project-name"] = "team-secrets";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var projectName = appBuilder.AddParameter("bitwarden-project-name");

        appBuilder.AddBitwardenSecretManager("bitwarden", projectName, Guid.NewGuid(), accessToken);

        using var app = appBuilder.Build();

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());

        Assert.Null(resource.RemoteProjectName);
        Assert.Same(projectName.Resource, resource.ConfiguredRemoteProjectNameParameter);
        Assert.Equal("bitwarden-project-name", resource.GetProjectNameDisplayValue());
    }

    [Fact]
    public void GetSecret_WhenManagedSecretExists_ReturnsManagedSecretResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken);
        var managedSecret = bitwarden.AddSecret("managed-secret");

        var reference = bitwarden.GetSecret("managed-secret");

        Assert.Same(managedSecret.Resource, reference.Resource);
        Assert.Single(bitwarden.Resource.DeclaredSecretReferences);
    }

    [Fact]
    public void WithAuthCacheDirectory_StoresConfiguredDirectory()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        const string authCacheDirectory = "./.state/bitwarden-auth";

        appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken)
            .WithAuthCacheDirectory(authCacheDirectory);

        using var app = appBuilder.Build();

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());

        Assert.Equal(authCacheDirectory, resource.AuthCacheDirectory);
    }

    [Fact]
    public void AddSecret_DuplicateRemoteName_Throws()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "shared-project", Guid.NewGuid(), accessToken);
        bitwarden.AddSecret("secret-a", "shared-secret");

        Action action = () => bitwarden.AddSecret("secret-b", "shared-secret");

        var exception = Assert.Throws<DistributedApplicationException>(action);
        Assert.Contains("shared-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithReference_InjectsStructuredConfiguration()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "management-access-token";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal(organizationId.ToString("D"), environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__OrganizationId"]);
        Assert.Equal(projectId.ToString("D"), environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__ProjectId"]);
        Assert.Equal("management-access-token", environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AccessToken"]);
        Assert.Equal(BitwardenSecretManagerResource.DefaultApiUrl, environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__ApiUrl"]);
        Assert.Equal(BitwardenSecretManagerResource.DefaultIdentityUrl, environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__IdentityUrl"]);
        Assert.False(environmentVariables.ContainsKey($"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheDirectory"));
    }

    [Fact]
    public async Task WithReference_WithAccessToken_OverridesAccessTokenInClient()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:management-token"] = "management-token-value";
        appBuilder.Configuration["Parameters:runtime-token"] = "runtime-token-value";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var managementToken = appBuilder.AddParameter("management-token", secret: true);
        var runtimeToken = appBuilder.AddParameter("runtime-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, managementToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden).WithBitwardenAccessToken(bitwarden, runtimeToken);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("runtime-token-value", environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AccessToken"]);
    }

    [Fact]
    public async Task WithReference_WithoutWithAccessToken_InjectsManagementToken()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:management-token"] = "management-token-value";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var managementToken = appBuilder.AddParameter("management-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, managementToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("management-token-value", environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AccessToken"]);
    }

    [Fact]
    public async Task WithAuthCacheDirectory_Parameter_InjectsAuthCachePathIntoApp()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string appAuthCacheDirectory = "/data/bitwarden";

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";
        appBuilder.Configuration["Parameters:bitwarden-auth-cache-location"] = appAuthCacheDirectory;

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var authCacheLocation = appBuilder.AddParameter("bitwarden-auth-cache-location");

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden).WithBitwardenAuthCacheDirectory(bitwarden, authCacheLocation);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal(appAuthCacheDirectory, environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheDirectory"]);
    }

    [Fact]
    public async Task WithAuthCacheVolume_DefaultArgs_MountsVolumeAndInjectsAuthCacheDirectory()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden).WithBitwardenAuthCacheVolume(bitwarden);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        // Env var points at the default path inside the container
        Assert.Equal(
            "/var/lib/bitwarden",
            environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheDirectory"]);

        // A volume mount is present for the default directory
        var mounts = consumer.Resource.Annotations.OfType<ContainerMountAnnotation>().ToList();
        var volumeMount = mounts.SingleOrDefault(m => m.Type == ContainerMountType.Volume && m.Target == "/var/lib/bitwarden");
        Assert.NotNull(volumeMount);
        Assert.Equal("consumer-bitwarden-bitwarden-auth", volumeMount.Source);
        Assert.False(volumeMount.IsReadOnly);
    }

    [Fact]
    public async Task WithAuthCacheVolume_CustomArgs_MountsVolumeAtCustomDirectory()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string customVolumeName = "shared-bw-auth";
        const string customDirectory = "/mnt/bitwarden";

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden).WithBitwardenAuthCacheVolume(bitwarden, volumeName: customVolumeName, containerDirectory: customDirectory);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal(
            customDirectory,
            environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheDirectory"]);

        var mounts = consumer.Resource.Annotations.OfType<ContainerMountAnnotation>().ToList();
        var volumeMount = mounts.SingleOrDefault(m => m.Type == ContainerMountType.Volume && m.Target == customDirectory);
        Assert.NotNull(volumeMount);
        Assert.Equal(customVolumeName, volumeMount.Source);
    }

    [Fact]
    public void WithAuthCacheVolume_OnNonContainerResource_Throws()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "my-project", Guid.NewGuid(), accessToken);

        // AddProject requires a project assembly reference; use ExecutableResource as a non-container stand-in.
        var nonContainer = appBuilder.AddExecutable("worker", "dotnet", ".");

        Assert.Throws<InvalidOperationException>(
            () => nonContainer.WithReference(bitwarden).WithBitwardenAuthCacheVolume(bitwarden));
    }

    [Fact]
    public async Task WithAuthCacheDirectory_String_InjectsAuthCachePathIntoApp()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string appAuthCacheDirectory = "/data/bitwarden";

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden).WithBitwardenAuthCacheDirectory(bitwarden, appAuthCacheDirectory);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal(appAuthCacheDirectory, environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheDirectory"]);
    }

    [Fact]
    public async Task WithAuthCacheDirectory_DoesNotInjectIntoApp()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var appHostAuthCacheDir = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}");

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken)
            .WithAuthCacheDirectory(appHostAuthCacheDir);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.False(environmentVariables.ContainsKey($"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheDirectory"));
    }

    [Fact]
    public async Task WithBitwardenSecretValue_InjectsResolvedSecretValue()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken);
        var managedSecret = bitwarden.AddSecret("managed-secret");

        Guid secretId = Guid.NewGuid();
        managedSecret.Resource.SecretId = secretId;
        bitwarden.Resource.BindResolvedSecret(secretId, managedSecret.Resource.RemoteName, "resolved-managed-value");

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithBitwardenSecretValue("DEMO_API_KEY", managedSecret.Resource);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("resolved-managed-value", environmentVariables["DEMO_API_KEY"]);
    }

    [Fact]
    public async Task WithBitwardenSecretId_InjectsResolvedSecretId()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken);
        var managedSecret = bitwarden.AddSecret("managed-secret");

        Guid secretId = Guid.NewGuid();
        managedSecret.Resource.SecretId = secretId;
        bitwarden.Resource.BindResolvedSecret(secretId, managedSecret.Resource.RemoteName, "resolved-managed-value");

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithBitwardenSecretId("DEMO_API_KEY_SECRET_ID", managedSecret.Resource);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal(secretId.ToString("D"), environmentVariables["DEMO_API_KEY_SECRET_ID"]);
    }

    [Fact]
    public void AddBitwardenSecretManager_RegistersResetAuthCacheCommand()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        appBuilder.AddBitwardenSecretManager("bitwarden", "test-project", Guid.NewGuid(), accessToken);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());

        var command = Assert.Single(
            resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == "reset-auth-cache");
        Assert.Equal("Reset auth cache", command.DisplayName);
        Assert.True(command.IsHighlighted);
    }

    [Fact]
    public void AddBitwardenSecretManager_RegistersReprovisionCommand()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        appBuilder.AddBitwardenSecretManager("bitwarden", "test-project", Guid.NewGuid(), accessToken);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());

        var command = Assert.Single(
            resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == KnownResourceCommands.RebuildCommand);
        Assert.Equal("Reprovision", command.DisplayName);
        Assert.False(command.IsHighlighted);
    }

    [Theory]
    [InlineData("NotStarted", ResourceCommandState.Enabled)]
    [InlineData("Waiting", ResourceCommandState.Disabled)]
    [InlineData("Running", ResourceCommandState.Disabled)]
    [InlineData("Finished", ResourceCommandState.Enabled)]
    [InlineData("FailedToStart", ResourceCommandState.Enabled)]
    [InlineData("Exited", ResourceCommandState.Enabled)]
    public void ResetAuthCacheCommand_UpdateState_ReturnsExpected(string resourceState, ResourceCommandState expected)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        appBuilder.AddBitwardenSecretManager("bitwarden", "test-project", Guid.NewGuid(), accessToken);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());
        var command = Assert.Single(
            resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == "reset-auth-cache");

        var actual = command.UpdateState(new UpdateCommandStateContext
        {
            ResourceSnapshot = new CustomResourceSnapshot { ResourceType = "BitwardenSecretManager", Properties = [], State = resourceState },
            ServiceProvider = app.Services
        });

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("NotStarted", ResourceCommandState.Disabled)]
    [InlineData("Waiting", ResourceCommandState.Enabled)]
    [InlineData("Running", ResourceCommandState.Enabled)]
    [InlineData("Finished", ResourceCommandState.Enabled)]
    [InlineData("FailedToStart", ResourceCommandState.Enabled)]
    [InlineData("Exited", ResourceCommandState.Enabled)]
    public void ReprovisionCommand_UpdateState_ReturnsExpected(string resourceState, ResourceCommandState expected)
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        appBuilder.AddBitwardenSecretManager("bitwarden", "test-project", Guid.NewGuid(), accessToken);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());
        var command = Assert.Single(
            resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == KnownResourceCommands.RebuildCommand);

        var actual = command.UpdateState(new UpdateCommandStateContext
        {
            ResourceSnapshot = new CustomResourceSnapshot { ResourceType = "BitwardenSecretManager", Properties = [], State = resourceState },
            ServiceProvider = app.Services
        });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task AddBitwardenSecretManager_CommandsAreInSnapshot()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        appBuilder.AddBitwardenSecretManager("bitwarden", "test-project", Guid.NewGuid(), accessToken);

        using var app = appBuilder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var snapshotTask = notifications.WatchAsync(cts.Token)
            .Where(e => e.Resource == resource)
            .FirstAsync(cts.Token);

        await notifications.PublishUpdateAsync(resource, s => s with { });

        var evt = await snapshotTask;

        Assert.Equal(2, evt.Snapshot.Commands.Length);
        Assert.Single(evt.Snapshot.Commands, c => c.Name == "reset-auth-cache");
        Assert.Single(evt.Snapshot.Commands, c => c.Name == KnownResourceCommands.RebuildCommand);
    }
}