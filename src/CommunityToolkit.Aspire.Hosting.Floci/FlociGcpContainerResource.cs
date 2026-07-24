#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Floci GCP emulator container.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class FlociGcpContainerResource(string name) : FlociContainerResource(name, "gcp")
{
    internal const int EndpointPort = 4588;
    internal const string HostnameEnvVar = "FLOCI_GCP_HOSTNAME";
    internal const string DefaultProjectIdEnvVar = "FLOCI_GCP_DEFAULT_PROJECT_ID";

    /// <summary>
    /// Gets the default GCP project ID configured for this Floci instance.
    /// Set by <see cref="FlociHostingExtension.AddFlociGcp"/> from the <c>defaultProjectId</c> parameter.
    /// </summary>
    internal string DefaultProjectId { get; init; } = "floci-local";

    internal override void ApplyUIEnvironment(EnvironmentCallbackContext context)
    {
        context.EnvironmentVariables[FlociUIContainerResource.GcpEndpointEnvVar] =
            ReferenceExpression.Create($"{PrimaryEndpoint}");
        context.EnvironmentVariables[FlociUIContainerResource.GcpProjectEnvVar] = DefaultProjectId;
    }

    internal override string DockerHostEnvVar => "FLOCI_GCP_DOCKER_DOCKER_HOST";
    internal override string StorageModeEnvVar => "FLOCI_GCP_STORAGE_MODE";
}

#pragma warning restore ASPIREATS001
