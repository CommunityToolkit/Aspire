using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning.Roles;

namespace CommunityToolkit.Aspire.Hosting.Azure.Dapr;
/// <summary>
/// Represents an annotation that defines a publishing action for Azure Dapr components.
/// </summary>
/// <remarks>This annotation is used to specify a custom action that is executed during the publishing process of
/// Azure Dapr components. The action is applied to the provided <see cref="AzureResourceInfrastructure"/> instance,
/// allowing customization of the resource infrastructure.</remarks>
/// <param name="PublishingAction">The action to be executed on the <see cref="AzureResourceInfrastructure"/> during the publishing process. This
/// action allows for customization of the infrastructure configuration.</param>
public record AzureDaprComponentPublishingAnnotation(Action<AzureResourceInfrastructure, UserAssignedIdentity> PublishingAction) : IResourceAnnotation;