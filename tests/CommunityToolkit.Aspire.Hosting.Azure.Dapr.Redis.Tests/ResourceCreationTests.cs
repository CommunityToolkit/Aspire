using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Aspire.Hosting.Azure;

using AzureRedisResource = Azure.Provisioning.Redis.RedisResource;

namespace CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void WithReference_WhenAADDisabled_UsesPasswordSecret()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureRedis("redisState")
                                .WithAccessKeyAuthentication()
                                .RunAsContainer();

        var daprState = builder.AddDaprStateStore("daprState")
            .WithReference(redisState);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var redisCache = Assert.Single(appModel.Resources.OfType<AzureRedisCacheResource>());

        string redisBicep = redisCache.GetBicepTemplateString();

        string expectedRedisBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param keyVaultName string

            resource redisState 'Microsoft.Cache/redis@2024-03-01' = {
              name: take('redisState-${uniqueString(resourceGroup().id)}', 63)
              location: location
              properties: {
                sku: {
                  name: 'Basic'
                  family: 'C'
                  capacity: 1
                }
                enableNonSslPort: false
                minimumTlsVersion: '1.2'
              }
              tags: {
                'aspire-resource-name': 'redisState'
              }
            }

            resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
              name: keyVaultName
            }

            resource connectionString 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
              name: 'connectionString'
              properties: {
                value: '${redisState.properties.hostName},ssl=true,password=${redisState.listKeys().primaryKey}'
              }
              parent: keyVault
            }

            resource daprRedisPassword 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
              name: 'daprRedisPassword'
              properties: {
                value: redisState.listKeys().primaryKey
              }
              parent: keyVault
            }

            output daprConnectionString string = '${redisState.properties.hostName}:${redisState.properties.sslPort}'

            output redisPasswordSecretUri string = daprRedisPassword.properties.secretUri
            """;

        Assert.Equal(NormalizeLineEndings(expectedRedisBicep), NormalizeLineEndings(redisBicep));


        var daprResource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

        string daprBicep = daprResource.GetBicepTemplateString();

        string expectedDaprBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param redisHost string

            param redisPasswordSecretUri string

            var resourceToken = uniqueString(resourceGroup().id)

            resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
              name: 'cae-${resourceToken}'
            }

            resource redisDaprState 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
              name: take('redisDaprState${resourceToken}', 24)
              properties: {
                componentType: 'state.redis'
                metadata: [
                  {
                    name: 'redisHost'
                    value: redisHost
                  }
                  {
                    name: 'enableTLS'
                    value: 'true'
                  }
                  {
                    name: 'actorStateStore'
                    value: 'true'
                  }
                  {
                    name: 'redisPassword'
                    secretRef: 'redisPassword'
                  }
                ]
                secrets: [
                  {
                    name: 'redisPassword'
                    keyVaultUrl: redisPasswordSecretUri
                  }
                ]
                version: 'v1'
              }
              parent: containerAppEnvironment
            }
            """;
        Assert.Equal(NormalizeLineEndings(expectedDaprBicep), NormalizeLineEndings(daprBicep));

    }

    [Fact]
    public void WithReference_WhenAADEnabled_SkipsPasswordSecret()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureRedis("redisState").RunAsContainer();

        var daprState = builder.AddDaprStateStore("daprState")
            .WithReference(redisState);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var redisCache = Assert.Single(appModel.Resources.OfType<AzureRedisCacheResource>());

        string redisBicep = redisCache.GetBicepTemplateString();

        string expectedRedisBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param principalId string

            param principalName string

            resource redisState 'Microsoft.Cache/redis@2024-03-01' = {
              name: take('redisState-${uniqueString(resourceGroup().id)}', 63)
              location: location
              properties: {
                sku: {
                  name: 'Basic'
                  family: 'C'
                  capacity: 1
                }
                enableNonSslPort: false
                disableAccessKeyAuthentication: true
                minimumTlsVersion: '1.2'
                redisConfiguration: {
                  'aad-enabled': 'true'
                }
              }
              tags: {
                'aspire-resource-name': 'redisState'
              }
            }

            resource redisState_contributor 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
              name: take('redisstatecontributor${uniqueString(resourceGroup().id)}', 24)
              properties: {
                accessPolicyName: 'Data Contributor'
                objectId: principalId
                objectIdAlias: principalName
              }
              parent: redisState
            }

            output connectionString string = '${redisState.properties.hostName},ssl=true'

            output daprConnectionString string = '${redisState.properties.hostName}:${redisState.properties.sslPort}'
            """;

        Assert.Equal(NormalizeLineEndings(expectedRedisBicep), NormalizeLineEndings(redisBicep));


        var daprResource = Assert.Single(appModel.Resources.OfType<AzureDaprComponentResource>());

        string daprBicep = daprResource.GetBicepTemplateString();

        string expectedDaprBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param redisHost string

            param principalId string

            var resourceToken = uniqueString(resourceGroup().id)

            resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
              name: 'cae-${resourceToken}'
            }

            resource redisDaprState 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
              name: take('redisDaprState${resourceToken}', 24)
              properties: {
                componentType: 'state.redis'
                metadata: [
                  {
                    name: 'redisHost'
                    value: redisHost
                  }
                  {
                    name: 'enableTLS'
                    value: 'true'
                  }
                  {
                    name: 'actorStateStore'
                    value: 'true'
                  }
                  {
                    name: 'useEntraID'
                    value: 'true'
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
            """;


        Assert.Equal(NormalizeLineEndings(expectedDaprBicep), NormalizeLineEndings(daprBicep));

    }

    [Fact]
    public void WithReference_WhenTLSDisabled_UsesNonSslPort()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureRedis("redisState")
                                .ConfigureInfrastructure(infr =>
                                {
                                    var redis = infr.GetProvisionableResources().OfType<AzureRedisResource>().Single();
                                    redis.EnableNonSslPort = true;
                                })
                                .RunAsContainer();

        var daprState = builder.AddDaprStateStore("daprState")
            .WithReference(redisState);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var redisCache = Assert.Single(appModel.Resources.OfType<AzureRedisCacheResource>());

        string redisBicep = redisCache.GetBicepTemplateString();


        string expectedRedisBicep = $$"""
            @description('The location for the resource(s) to be deployed.')
            param location string = resourceGroup().location

            param principalId string

            param principalName string

            resource redisState 'Microsoft.Cache/redis@2024-03-01' = {
              name: take('redisState-${uniqueString(resourceGroup().id)}', 63)
              location: location
              properties: {
                sku: {
                  name: 'Basic'
                  family: 'C'
                  capacity: 1
                }
                enableNonSslPort: true
                disableAccessKeyAuthentication: true
                minimumTlsVersion: '1.2'
                redisConfiguration: {
                  'aad-enabled': 'true'
                }
              }
              tags: {
                'aspire-resource-name': 'redisState'
              }
            }

            resource redisState_contributor 'Microsoft.Cache/redis/accessPolicyAssignments@2024-03-01' = {
              name: take('redisstatecontributor${uniqueString(resourceGroup().id)}', 24)
              properties: {
                accessPolicyName: 'Data Contributor'
                objectId: principalId
                objectIdAlias: principalName
              }
              parent: redisState
            }

            output connectionString string = '${redisState.properties.hostName},ssl=true'

            output daprConnectionString string = '${redisState.properties.hostName}:${redisState.properties.port}'
            """;


        Assert.Equal(NormalizeLineEndings(expectedRedisBicep), NormalizeLineEndings(redisBicep));
    }

    [Fact]
    public void WithReference_WhenNonStateType_ThrowsException()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redisState = builder.AddAzureRedis("redisState").RunAsContainer();
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var daprPubSub = builder.AddDaprPubSub("daprState")
                .WithReference(redisState);
        });

        Assert.Contains("Unsupported Dapr component type: pubsub", ex.Message);
    }
    public static string NormalizeLineEndings(string input)
    {
        return input.Replace("\r\n", "\n");
    }

}
