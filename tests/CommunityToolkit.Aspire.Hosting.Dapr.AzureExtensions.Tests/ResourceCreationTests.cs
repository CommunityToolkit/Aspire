using Aspire.Hosting;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Primitives;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.Dapr.AzureExtensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void AddAzureDaprResource_AddsToAppBuilder()
    {
        var builder = DistributedApplication.CreateBuilder();

        var daprStateBuilder = builder.AddDaprStateStore("daprState").AddAzureDaprResource("AzureDaprResource", _ =>
        {
            // no-op
        });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<AzureDaprComponentResource>().SingleOrDefault();

        resource.Should().NotBeNull();
    }

    [Fact]
    public void CreateDaprComponent_ReturnsPopulatedComponent()
    {

        var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");

        daprResource.Should().NotBeNull();

        daprResource.BicepIdentifier.Should().Be("daprComponent");
        daprResource.ComponentType.Should().BeEquivalentTo(new BicepValue<string>("state.redis"));
        daprResource.Version.Should().BeEquivalentTo(new BicepValue<string>("v1"));
    }
    [Fact]
    public void GetInfrastructureConfigurationAction_AddsContainerAppEnv_AndDaprComponent_AndParameters()
    {
        var builder = DistributedApplication.CreateBuilder();

        var redisHost = new ProvisioningParameter("daprConnectionString", typeof(string));
        var daprResource = AzureDaprHostingExtensions.CreateDaprComponent("daprComponent", "state.redis", "v1");
        var configureInfrastructure = AzureDaprHostingExtensions.GetInfrastructureConfigurationAction(daprResource, [redisHost]);

        var azureDaprResourceBuilder = builder.AddDaprStateStore("daprState")
                 .AddAzureDaprResource("AzureDaprResource", configureInfrastructure);


        // azureDaprResourceBuilder.ConfigureInfrastructure(infr =>
        // {
        //     var resources = infr.GetProvisionableResources();

        //     resources.OfType<ContainerAppManagedEnvironmentDaprComponent>().Should().HaveCount(2);
        //     resources.OfType<ProvisioningParameter>()
        //              .FirstOrDefault(_ => _.BicepIdentifier == "daprConnectionString")
        //              .Should()
        //              .NotBeNull();
        // });

    }

    [Fact]
    public void GetInfrastructureConfigurationAction_HandlesNullParameters()
    {

    }

    [Fact]
    public void ConfigureKeyVaultSecrets_AddsKeyVaultNameParameterAndService_AndSecrets()
    {

    }

    [Fact]
    public void ConfigureKeyVaultSecrets_HandlesNullSecrets()
    {

    }

}

