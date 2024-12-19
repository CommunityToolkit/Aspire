using Aspire.Hosting.Azure;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an Azure Dapr resource.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AzureDaprResource"/> class.
/// </remarks>
/// <param name="name">The name of the resource.</param>
#pragma warning disable RS0016 // Add public types and members to the declared API
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class AzureDaprResource(string name) :
                              // AzureProvisioningResource(name, configureInfrastructure), 
IResource
{
    public string Name => name;

    public ResourceAnnotationCollection Annotations => [];
}
#pragma warning restore RS0016 // Add public types and members to the declared API
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

