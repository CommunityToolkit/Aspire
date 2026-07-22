#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Floci AWS emulator container.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class FlociAwsContainerResource(string name) : FlociContainerResource(name, AwsEndpointName)
{
    internal const int AwsEndpointPort = 4566;
    internal const string AwsEndpointName = "aws";
    internal const string HostnameEnvVar = "FLOCI_HOSTNAME";
    internal const string DefaultRegionEnvVar = "FLOCI_DEFAULT_REGION";
    internal const string DefaultAccountIdEnvVar = "FLOCI_DEFAULT_ACCOUNT_ID";

    // Quarkus JVM Docker image config override path
    internal const string ConfigMountPath = "/deployments/config/application.yml";

    /// <summary>
    /// Gets the AWS region configured for this Floci instance.
    /// Set by <see cref="FlociHostingExtension.AddFlociAws"/> from the <c>defaultRegion</c> parameter.
    /// Used by the <c>BeforeStartEvent</c> subscriber to inject <c>AWS_DEFAULT_REGION</c> into dependent resources.
    /// </summary>
    internal string DefaultRegion { get; init; } = "us-east-1";

    /// <summary>
    /// Gets the default AWS account ID configured for this Floci instance.
    /// Set by <see cref="FlociHostingExtension.AddFlociAws"/> from the <c>defaultAccountId</c> parameter.
    /// Used by <c>WithFlociUI</c> to inject <c>FLOCI_DEFAULT_ACCOUNT_ID</c> into the UI container.
    /// </summary>
    internal string DefaultAccountId { get; init; } = "000000000000";

    internal override void ApplyUIEnvironment(EnvironmentCallbackContext context)
    {
        // Floci serves HTTP on the same port (4566), so the http:// endpoint URL stays valid.
        context.EnvironmentVariables[FlociUIContainerResource.EndpointEnvVar] =
            ReferenceExpression.Create($"{PrimaryEndpoint}");
        context.EnvironmentVariables[FlociUIContainerResource.RegionEnvVar] = DefaultRegion;
        context.EnvironmentVariables[FlociUIContainerResource.AccessKeyIdEnvVar] = "test";
        context.EnvironmentVariables[FlociUIContainerResource.SecretAccessKeyEnvVar] = "test";
        context.EnvironmentVariables[FlociUIContainerResource.DefaultAccountIdEnvVar] = DefaultAccountId;
    }

    internal override string DockerHostEnvVar => "FLOCI_DOCKER_DOCKER_HOST";
    internal override string StorageModeEnvVar => "FLOCI_STORAGE_MODE";
}

#pragma warning restore ASPIREATS001
