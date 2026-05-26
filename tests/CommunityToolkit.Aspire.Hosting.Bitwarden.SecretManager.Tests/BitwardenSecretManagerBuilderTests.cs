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
    public void AddSecret_ParameterValue_WhenBuilderIsNull_Throws()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:managed-secret"] = "managed-secret-value";

        IResourceBuilder<BitwardenSecretManagerResource> builder = null!;
        var value = appBuilder.AddParameter("managed-secret", secret: true);

        Action action = () => BitwardenSecretManagerExtensions.AddSecret(builder, "managed-secret", value);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddSecret_ReferenceValue_WhenBuilderIsNull_Throws()
    {
        IResourceBuilder<BitwardenSecretManagerResource> builder = null!;
        ReferenceExpression value = ReferenceExpression.Create($"test-value");

        Action action = () => BitwardenSecretManagerExtensions.AddSecret(builder, "managed-secret", value);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddBitwardenSecretManager_StoresConfiguredProjectName()
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
        Assert.Equal(BitwardenSecretManagerResource.DefaultApiUrl, resource.GetApiUrlOrDefault());
        Assert.Equal(BitwardenSecretManagerResource.DefaultIdentityUrl, resource.GetIdentityUrlOrDefault());
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
        appBuilder.Configuration["Parameters:managed-secret"] = "secret-value";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var managedSecretValue = appBuilder.AddParameter("managed-secret", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken);
        var managedSecret = bitwarden.AddSecret("managed-secret", managedSecretValue);

        var reference = bitwarden.GetSecret("managed-secret");

        Assert.Same(managedSecret.Resource, reference);
        Assert.Single(bitwarden.Resource.DeclaredSecretReferences);
    }

    [Fact]
    public void WithAuthCacheFile_StoresConfiguredPath()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        const string authCachePath = "./.state/bitwarden-auth.bin";

        appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken)
            .WithAuthCacheFile(authCachePath);

        using var app = appBuilder.Build();

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<BitwardenSecretManagerResource>());

        Assert.Equal(authCachePath, resource.AuthCacheFile);
    }

    [Fact]
    public void AddSecret_DuplicateRemoteName_Throws()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        appBuilder.Configuration["Parameters:secret-a"] = "value-a";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var secretValue = appBuilder.AddParameter("secret-a", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "shared-project", Guid.NewGuid(), accessToken);
        bitwarden.AddSecret("secret-a", "shared-secret", secretValue);

        Action action = () => bitwarden.AddSecret("secret-b", "shared-secret", secretValue);

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
        Assert.False(environmentVariables.ContainsKey($"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheFile"));
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
        consumer.WithReference(bitwarden, bw => bw.WithAccessToken(runtimeToken));

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
    public async Task WithAuthCacheFile_Parameter_InjectsAuthCacheFileIntoApp()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string appAuthCachePath = "/data/bitwarden/auth-cache";

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";
        appBuilder.Configuration["Parameters:bitwarden-auth-cache-location"] = appAuthCachePath;

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var authCacheLocation = appBuilder.AddParameter("bitwarden-auth-cache-location");

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden, bw => bw.WithAuthCacheFile(authCacheLocation));

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal(appAuthCachePath, environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheFile"]);
    }

    [Fact]
    public async Task WithAuthCacheFile_String_InjectsAuthCacheFileIntoApp()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string appAuthCachePath = "/data/bitwarden/auth-cache";

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden, bw => bw.WithAuthCacheFile(appAuthCachePath));

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal(appAuthCachePath, environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheFile"]);
    }

    [Fact]
    public async Task WithAuthCacheFile_DoesNotInjectIntoApp()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var appHostAuthCachePath = Path.Combine(Path.GetTempPath(), $"bitwarden-{Guid.NewGuid():N}.auth-cache");

        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-organization-id"] = organizationId.ToString("D");
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";

        var organizationParameter = appBuilder.AddParameter("bitwarden-organization-id");
        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "consumer-project", organizationParameter, accessToken)
            .WithAuthCacheFile(appHostAuthCachePath);
        bitwarden.Resource.BindResolvedProjectId(projectId);

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden);

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.False(environmentVariables.ContainsKey($"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AuthCacheFile"));
    }

    [Fact]
    public async Task WithBitwardenSecretValue_InjectsResolvedSecretValue()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "access-token";
        appBuilder.Configuration["Parameters:managed-secret"] = "managed-value";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var managedSecretValue = appBuilder.AddParameter("managed-secret", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken);
        var managedSecret = bitwarden.AddSecret("managed-secret", managedSecretValue);

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
        appBuilder.Configuration["Parameters:managed-secret"] = "managed-value";

        var accessToken = appBuilder.AddParameter("bitwarden-access-token", secret: true);
        var managedSecretValue = appBuilder.AddParameter("managed-secret", secret: true);

        var bitwarden = appBuilder.AddBitwardenSecretManager("bitwarden", "managed-project", Guid.NewGuid(), accessToken);
        var managedSecret = bitwarden.AddSecret("managed-secret", managedSecretValue);

        Guid secretId = Guid.NewGuid();
        managedSecret.Resource.SecretId = secretId;
        bitwarden.Resource.BindResolvedSecret(secretId, managedSecret.Resource.RemoteName, "resolved-managed-value");

        var consumer = appBuilder.AddContainer("consumer", "busybox", "1.37.0");
        consumer.WithReference(bitwarden, bw => bw.WithBitwardenSecretId("DEMO_API_KEY_SECRET_ID", managedSecret.Resource));

        using var app = appBuilder.Build();

        var environmentVariables = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal(secretId.ToString("D"), environmentVariables["DEMO_API_KEY_SECRET_ID"]);
    }
}