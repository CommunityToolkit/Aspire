﻿using Aspire.Hosting;
using Aspire.Hosting.Azure;
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

    var resource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

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
            """;

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
              name: take(toLower('daprComponent${resourceToken}'), 60)
              properties: {
                componentType: 'state.redis'
                version: 'v1'
              }
              parent: containerAppEnvironment
            }
            """;

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
              name: take(toLower('daprComponent${resourceToken}'), 60)
              properties: {
                componentType: 'state.redis'
                version: 'v1'
              }
              parent: containerAppEnvironment
            }
            """;

    Assert.Equal(expectedBicep, bicepTemplate);
  }

  [Fact]
  public void ConfigureKeyVaultSecretsComponent_AddsKeyVaultSecretsComponent()
  {
    using var builder = TestDistributedApplicationBuilder.Create();

    var redisHost = new ProvisioningParameter("daprConnectionString", typeof(string));
    var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");
    var configureInfrastructure = AzureDaprHostingExtensions.GetInfrastructureConfigurationAction(daprResource, [redisHost]);

    var keyVaultName = new ProvisioningParameter(AzureBicepResource.KnownParameters.KeyVaultName, typeof(string));
    
    var azureDaprResourceBuilder = builder.AddDaprStateStore("daprState")
                                          .ConfigureKeyVaultSecretsComponent(keyVaultName);

    using var app = builder.Build();

    var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

    var resource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

    string bicepTemplate = resource.GetBicepTemplateString();

    string expectedBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param principalId string

            param keyVaultName string

            var resourceToken = uniqueString(resourceGroup().id)

            resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
              name: 'cae-${resourceToken}'
            }

            resource secretStore 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
              name: take(toLower('secretStore${resourceToken}'), 60)
              properties: {
                componentType: 'secretstores.azure.keyvault'
                metadata: [
                  {
                    name: 'vaultName'
                    value: keyVaultName
                  }
                  {
                    name: 'azureClientId'
                    value: principalId
                  }
                ]
                version: 'v1'
              }
              parent: containerAppEnvironment
            }

            output secretStoreComponent string = take(toLower('secretStore${resourceToken}'), 60)
            """;

    Assert.Equal(expectedBicep, bicepTemplate);
  }


}

