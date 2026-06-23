using CommunityToolkit.Aspire.Bitwarden.SecretManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Bitwarden.SecretManager.Tests;

public class AspireBitwardenSecretManagerExtensionsTests
{
    [Fact]
    public void AddBitwardenSecretManagerClient_BindsSettings()
    {
        var builder = CreateBuilder([
            ("bitwarden", Guid.NewGuid(), Guid.NewGuid(), "access-token"),
        ]);

        builder.AddBitwardenSecretManagerClient("bitwarden", settings => settings.DisableHealthChecks = true);

        using var host = builder.Build();

        var settings = host.Services.GetRequiredService<BitwardenSecretManagerClientSettings>();

        Assert.Equal("access-token", settings.AccessToken);
        Assert.Equal("https://api.bitwarden.example", settings.ApiUrl);
        Assert.Equal("https://identity.bitwarden.example", settings.IdentityUrl);
    }

    [Fact]
    public void AddKeyedBitwardenSecretManagerClient_BindsKeyedSettings()
    {
        var firstOrganizationId = Guid.NewGuid();
        var firstProjectId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();

        var builder = CreateBuilder([
            ("bitwarden-first", firstOrganizationId, firstProjectId, "first-token"),
            ("bitwarden-second", secondOrganizationId, secondProjectId, "second-token"),
        ]);

        builder.AddKeyedBitwardenSecretManagerClient("bitwarden-first", settings => settings.DisableHealthChecks = true);
        builder.AddKeyedBitwardenSecretManagerClient("bitwarden-second", settings => settings.DisableHealthChecks = true);

        using var host = builder.Build();

        var firstSettings = host.Services.GetRequiredKeyedService<BitwardenSecretManagerClientSettings>("bitwarden-first");
        var secondSettings = host.Services.GetRequiredKeyedService<BitwardenSecretManagerClientSettings>("bitwarden-second");

        Assert.Equal(firstOrganizationId, firstSettings.OrganizationId);
        Assert.Equal(firstProjectId, firstSettings.ProjectId);
        Assert.Equal("first-token", firstSettings.AccessToken);
        Assert.Equal(secondOrganizationId, secondSettings.OrganizationId);
        Assert.Equal(secondProjectId, secondSettings.ProjectId);
        Assert.Equal("second-token", secondSettings.AccessToken);
    }

    [Fact]
    public void AddBitwardenSecretManagerClients_KeyedAndUnkeyedCanCoexist()
    {
        var firstOrganizationId = Guid.NewGuid();
        var firstProjectId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();

        var builder = CreateBuilder([
            ("bitwarden", firstOrganizationId, firstProjectId, "first-token"),
            ("bitwarden-second", secondOrganizationId, secondProjectId, "second-token"),
        ]);

        builder.AddBitwardenSecretManagerClient("bitwarden", settings => settings.DisableHealthChecks = true);
        builder.AddKeyedBitwardenSecretManagerClient("bitwarden-second", settings => settings.DisableHealthChecks = true);

        using var host = builder.Build();

        var firstSettings = host.Services.GetRequiredService<BitwardenSecretManagerClientSettings>();
        var secondSettings = host.Services.GetRequiredKeyedService<BitwardenSecretManagerClientSettings>("bitwarden-second");

        Assert.Equal(firstOrganizationId, firstSettings.OrganizationId);
        Assert.Equal(secondOrganizationId, secondSettings.OrganizationId);
        Assert.Equal(secondProjectId, secondSettings.ProjectId);
        Assert.Equal("second-token", secondSettings.AccessToken);
    }

    [Fact]
    public void AddBitwardenSecretManagerClient_HealthCheckShouldBeRegisteredWhenEnabled()
    {
        var builder = CreateBuilder([
            ("bitwarden", Guid.NewGuid(), Guid.NewGuid(), "access-token"),
        ]);

        builder.AddBitwardenSecretManagerClient("bitwarden", settings => settings.DisableHealthChecks = false);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        Assert.NotNull(healthCheckService);
    }

    [Fact]
    public void AddBitwardenSecretManagerClient_HealthCheckShouldNotBeRegisteredWhenDisabled()
    {
        var builder = CreateBuilder([
            ("bitwarden", Guid.NewGuid(), Guid.NewGuid(), "access-token"),
        ]);

        builder.AddBitwardenSecretManagerClient("bitwarden", settings => settings.DisableHealthChecks = true);

        using var host = builder.Build();

        var healthCheckService = host.Services.GetService<HealthCheckService>();

        Assert.Null(healthCheckService);
    }

    private static HostApplicationBuilder CreateBuilder((string Name, Guid OrganizationId, Guid ProjectId, string AccessToken)[] connections)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        List<KeyValuePair<string, string?>> values = [];
        foreach ((string name, Guid organizationId, Guid projectId, string accessToken) in connections)
        {
            values.Add(new($"Aspire:Bitwarden:SecretManager:{name}:OrganizationId", organizationId.ToString("D")));
            values.Add(new($"Aspire:Bitwarden:SecretManager:{name}:ProjectId", projectId.ToString("D")));
            values.Add(new($"Aspire:Bitwarden:SecretManager:{name}:AccessToken", accessToken));
            values.Add(new($"Aspire:Bitwarden:SecretManager:{name}:ApiUrl", "https://api.bitwarden.example"));
            values.Add(new($"Aspire:Bitwarden:SecretManager:{name}:IdentityUrl", "https://identity.bitwarden.example"));
        }

        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }
}