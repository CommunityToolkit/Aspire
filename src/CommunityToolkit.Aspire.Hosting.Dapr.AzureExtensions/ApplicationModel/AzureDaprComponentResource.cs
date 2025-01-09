using Aspire.Hosting.Azure;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an Azure Dapr component resource.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AzureDaprComponentResource"/> class.
/// </remarks>
/// <param name="bicepIdentifier">The Bicep identifier.</param>
/// <param name="configureInfrastructure">The action to configure the Azure resource infrastructure.</param>
public class AzureDaprComponentResource(string bicepIdentifier, Action<AzureResourceInfrastructure> configureInfrastructure) : AzureProvisioningResource(bicepIdentifier, configureInfrastructure);