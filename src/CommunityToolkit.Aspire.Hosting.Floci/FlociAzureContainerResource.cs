#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Floci Azure emulator container.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class FlociAzureContainerResource(string name) : FlociContainerResource(name, "azure")
{
    internal const int EndpointPort = 4577;
    internal const string HostnameEnvVar = "FLOCI_AZ_HOSTNAME";

    // Well-known Azurite-compatible dev credentials that floci-az accepts by default (no auth enforced).
    internal const string DefaultAccountName = "devstoreaccount1";
    internal const string DefaultAccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMh0==";

    internal override void ApplyUIEnvironment(EnvironmentCallbackContext context)
    {
        context.EnvironmentVariables[FlociUIContainerResource.AzureEndpointEnvVar] =
            ReferenceExpression.Create($"{PrimaryEndpoint}");
        context.EnvironmentVariables[FlociUIContainerResource.AzureAccountNameEnvVar] = DefaultAccountName;
    }

    internal override string DockerHostEnvVar => "FLOCI_AZ_DOCKER_DOCKER_HOST";
    internal override string StorageModeEnvVar => "FLOCI_AZ_STORAGE_MODE";
}

#pragma warning restore ASPIREATS001
