using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Azure.Provisioning;
using Azure.Provisioning.KeyVault;

namespace CommunityToolkit.Aspire.Hosting.Dapr.AzureExtensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void AddAzureDaprResource_AddsToAppBuilder()
    {
        var builder = DistributedApplication.CreateBuilder();

        var daprStateBuilder = builder.AddDaprStateStore("daprState")
                                      .AddAzureDaprResource("AzureDaprResource", _ =>
                                      {
                                          // no-op
                                      });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<AzureDaprComponentResource>().SingleOrDefault();

        Assert.NotNull(resource);
    }

    [Fact]
    public void CreateDaprComponent_ReturnsPopulatedComponent()
    {
        var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");

        Assert.NotNull(daprResource);
        Assert.Equal("daprComponent", daprResource.BicepIdentifier);
        Assert.Equal("state.redis", daprResource.ComponentType.Value);
        Assert.Equal("v1", daprResource.Version.Value);
    }

    [Fact]
    public void GetInfrastructureConfigurationAction_ComponentNameCanBeOverwritten()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var redisHost = new ProvisioningParameter("daprConnectionString", typeof(string));
        var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");
        var configureInfrastructure = AzureDaprHostingExtensions.GetInfrastructureConfigurationAction(daprResource, [redisHost]);

        daprResource.Name = "myDaprComponent";
        
        var azureDaprResourceBuilder = builder.AddDaprStateStore("daprState")
                 .AddAzureDaprResource("AzureDaprResource", configureInfrastructure);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

        string bicepTemplate = resource.GetBicepTemplateString();

        string expectedBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param daprConnectionString string

            var resourceToken = uniqueString(resourceGroup().id)

            resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
              name: 'cae-${resourceToken}'
            }

            resource daprComponent 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
              name: 'myDaprComponent'
              properties: {
                componentType: 'state.redis'
                version: 'v1'
              }
              parent: containerAppEnvironment
            }
            """.ReplaceLineEndings("\n");

        Assert.Equal(expectedBicep, bicepTemplate);
    }

    [Fact]
    public void GetInfrastructureConfigurationAction_AddsContainerAppEnv_AndDaprComponent_AndParametersAsync()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var redisHost = new ProvisioningParameter("daprConnectionString", typeof(string));
        var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");
        var configureInfrastructure = AzureDaprHostingExtensions.GetInfrastructureConfigurationAction(daprResource, [redisHost]);

        var azureDaprResourceBuilder = builder.AddDaprStateStore("daprState")
                 .AddAzureDaprResource("AzureDaprResource", configureInfrastructure);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

        string bicepTemplate = resource.GetBicepTemplateString();

        string expectedBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param daprConnectionString string

            var resourceToken = uniqueString(resourceGroup().id)

            resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
              name: 'cae-${resourceToken}'
            }

            resource daprComponent 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
              name: take('daprComponent${resourceToken}', 24)
              properties: {
                componentType: 'state.redis'
                version: 'v1'
              }
              parent: containerAppEnvironment
            }
            """.ReplaceLineEndings("\n");

        Assert.Equal(expectedBicep, bicepTemplate);
    }

    [Fact]
    public void GetInfrastructureConfigurationAction_HandlesNullParameters()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");
        var configureInfrastructure = AzureDaprHostingExtensions.GetInfrastructureConfigurationAction(daprResource);

        var azureDaprResourceBuilder = builder.AddDaprStateStore("daprState")
                 .AddAzureDaprResource("AzureDaprResource", configureInfrastructure);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

        string bicepTemplate = resource.GetBicepTemplateString();

        string expectedBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            var resourceToken = uniqueString(resourceGroup().id)

            resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
              name: 'cae-${resourceToken}'
            }

            resource daprComponent 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
              name: take('daprComponent${resourceToken}', 24)
              properties: {
                componentType: 'state.redis'
                version: 'v1'
              }
              parent: containerAppEnvironment
            }
            """.ReplaceLineEndings("\n");

        Assert.Equal(expectedBicep, bicepTemplate);
    }

    [Fact]
    public void ConfigureKeyVaultSecrets_AddsKeyVaultNameParameterAndService_AndSecrets()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var redisHost = new ProvisioningParameter("daprConnectionString", typeof(string));
        var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");
        var configureInfrastructure = AzureDaprHostingExtensions.GetInfrastructureConfigurationAction(daprResource, [redisHost]);

        var azureDaprResourceBuilder = builder.AddDaprStateStore("daprState")
                 .AddAzureDaprResource("AzureDaprResource", (infra) =>
                 {
                     configureInfrastructure(infra);
                     infra.ConfigureKeyVaultSecrets([
                        new KeyVaultSecret("mysecret")
                        {
                            Name = "mysecret",
                            Properties = new SecretProperties
                            {
                                Value = "secretValue"
                            }
                        }
                    ]);
                 });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

        string bicepTemplate = resource.GetBicepTemplateString();

        string expectedBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param daprConnectionString string

            param keyVaultName string

            var resourceToken = uniqueString(resourceGroup().id)

            resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
              name: 'cae-${resourceToken}'
            }

            resource daprComponent 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
              name: take('daprComponent${resourceToken}', 24)
              properties: {
                componentType: 'state.redis'
                version: 'v1'
              }
              parent: containerAppEnvironment
            }

            resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
              name: keyVaultName
            }

            resource mysecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
              name: 'mysecret'
              properties: {
                value: 'secretValue'
              }
              parent: keyVault
            }
            """.ReplaceLineEndings("\n");

        Assert.Equal(expectedBicep, bicepTemplate);
    }

    [Fact]
    public void ConfigureKeyVaultSecrets_HandlesNullSecrets()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var redisHost = new ProvisioningParameter("daprConnectionString", typeof(string));
        var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");
        var configureInfrastructure = AzureDaprHostingExtensions.GetInfrastructureConfigurationAction(daprResource, [redisHost]);

        var azureDaprResourceBuilder = builder.AddDaprStateStore("daprState")
                 .AddAzureDaprResource("AzureDaprResource", (infra) =>
                 {
                     configureInfrastructure(infra);
                     infra.ConfigureKeyVaultSecrets();
                 });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

        string bicepTemplate = resource.GetBicepTemplateString();

        string expectedBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param daprConnectionString string

            param keyVaultName string

            var resourceToken = uniqueString(resourceGroup().id)

            resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
              name: 'cae-${resourceToken}'
            }

            resource daprComponent 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
              name: take('daprComponent${resourceToken}', 24)
              properties: {
                componentType: 'state.redis'
                version: 'v1'
              }
              parent: containerAppEnvironment
            }

            resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
              name: keyVaultName
            }
            """.ReplaceLineEndings("\n");

        Assert.Equal(expectedBicep, bicepTemplate);
    }

}

