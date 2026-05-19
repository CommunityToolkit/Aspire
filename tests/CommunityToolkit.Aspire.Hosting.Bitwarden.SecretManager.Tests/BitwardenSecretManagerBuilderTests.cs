using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

public class BitwardenSecretManagerBuilderTests
{
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
        appBuilder.Configuration["Parameters:bitwarden-access-token"] = "runtime-access-token";

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
        Assert.Equal("runtime-access-token", environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__AccessToken"]);
        Assert.Equal(BitwardenSecretManagerResource.DefaultApiUrl, environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__ApiUrl"]);
        Assert.Equal(BitwardenSecretManagerResource.DefaultIdentityUrl, environmentVariables[$"{BitwardenSecretManagerResource.ConfigurationKeyPrefix}__bitwarden__IdentityUrl"]);
    }
}