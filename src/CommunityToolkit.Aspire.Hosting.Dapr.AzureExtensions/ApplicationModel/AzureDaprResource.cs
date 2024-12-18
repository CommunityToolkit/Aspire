using Aspire.Hosting.Azure;

namespace Aspire.Hosting.ApplicationModel;
public class AzureDaprResource : AzureProvisioningResource, IResource
{
    public AzureDaprResource(string name, Action<AzureResourceInfrastructure> configureInfrastructure)
        : base(name, configureInfrastructure)
    {

    }

}
