using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Azure.AppContainers;
using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.Authorization;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Roles;
using CommunityToolkit.Aspire.Hosting.Azure.Dapr;
using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Azure Container App Environment resources.
/// </summary>
public static class AzureContainerAppEnvironmentResourceBuilderExtensions
{
    private const string DaprManagedIdentityKey = "daprManagedIdentity";

    /// <summary>
    /// Configures the Azure Container App Environment resource to use Dapr.
    /// This method creates a dedicated managed identity for Dapr components and configures all Dapr components to use it.
    /// </summary>
    /// <param name="builder">The Azure Container App Environment resource builder.</param>
    /// <returns>The configured Azure Container App Environment resource builder.</returns>
    public static IResourceBuilder<AzureContainerAppEnvironmentResource> WithDaprComponents(
        this IResourceBuilder<AzureContainerAppEnvironmentResource> builder)
    {
        builder.ApplicationBuilder.AddDapr(c =>
       {
           c.PublishingConfigurationAction = (IResource resource, DaprSidecarOptions? daprSidecarOptions) =>
           {
               var configureAction = (AzureResourceInfrastructure infrastructure, ContainerApp containerApp) =>
               {
                   containerApp.Configuration.Dapr = new ContainerAppDaprConfiguration
                   {
                       AppId = daprSidecarOptions?.AppId ?? resource.Name,
                       AppPort = daprSidecarOptions?.AppPort ?? 8080,
                       IsApiLoggingEnabled = daprSidecarOptions?.EnableApiLogging ?? false,
                       LogLevel = daprSidecarOptions?.LogLevel?.ToLower() switch
                       {
                           "debug" => ContainerAppDaprLogLevel.Debug,
                           "warn" => ContainerAppDaprLogLevel.Warn,
                           "error" => ContainerAppDaprLogLevel.Error,
                           _ => ContainerAppDaprLogLevel.Info
                       },
                       AppProtocol = daprSidecarOptions?.AppProtocol?.ToLower() switch
                       {
                           "grpc" => ContainerAppProtocol.Grpc,
                           _ => ContainerAppProtocol.Http,
                       },
                       IsEnabled = true
                   };
               };

               resource.Annotations.Add(new AzureContainerAppCustomizationAnnotation(configureAction));
           };
       });

        return builder.ConfigureInfrastructure(infrastructure =>
        {
            // Create the Dapr managed identity once
            var daprIdentity = new UserAssignedIdentity(DaprManagedIdentityKey);

            infrastructure.Add(daprIdentity);

            var daprComponentResources = builder.ApplicationBuilder.Resources.OfType<IDaprComponentResource>();

            foreach (var daprComponentResource in daprComponentResources)
            {
                if (daprComponentResource.TryGetLastAnnotation<RoleAssignmentAnnotation>(out var roleAssignmentAnnotation))
                {
                    var target = roleAssignmentAnnotation.Target.AddAsExistingResource(infrastructure);

                    foreach (var roleDefinition in roleAssignmentAnnotation.Roles)
                    {
                        var id = new MemberExpression(new IdentifierExpression(roleAssignmentAnnotation.Target.GetBicepIdentifier()), "id");
                        var roleAssignment = new RoleAssignment($"{daprComponentResource.Name}{roleDefinition.Name}")
                        {
                            Name = BicepFunction.CreateGuid(id, daprIdentity.Id, BicepFunction.GetSubscriptionResourceId("Microsoft.Authorization/roleDefinitions", roleDefinition.Id)),
                            RoleDefinitionId = BicepFunction.GetSubscriptionResourceId("Microsoft.Authorization/roleDefinitions", roleDefinition.Id),
                            PrincipalId = daprIdentity.PrincipalId,
                            PrincipalType = RoleManagementPrincipalType.ServicePrincipal,
                            Scope =  new IdentifierExpression(target.BicepIdentifier)
                        };

                        infrastructure.Add(roleAssignment);
                    }
                }

                daprComponentResource.TryGetLastAnnotation<AzureDaprComponentPublishingAnnotation>(out var publishingAnnotation);
                publishingAnnotation?.PublishingAction(infrastructure, daprIdentity);


            }
        });
    }
}
