#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Floci UI web console container.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="floci">The <see cref="FlociContainerResource"/> this UI instance connects to.</param>
[AspireExport(ExposeProperties = true)]
public class FlociUIContainerResource(string name, FlociContainerResource floci)
    : ContainerResource(name), IResourceWithParent<FlociContainerResource>
{
    // The packaged floci/floci-ui image serves both the SPA and its API from a single
    // server listening on PORT (default 4500).
    internal const int UIPort = 4500;
    internal const string PrimaryEndpointName = "http";
    internal const string EndpointEnvVar = "FLOCI_ENDPOINT";
    internal const string RegionEnvVar = "AWS_REGION";
    internal const string AccessKeyIdEnvVar = "AWS_ACCESS_KEY_ID";
    internal const string SecretAccessKeyEnvVar = "AWS_SECRET_ACCESS_KEY";
    internal const string DefaultAccountIdEnvVar = "FLOCI_DEFAULT_ACCOUNT_ID";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the Floci resource this UI instance connects to.
    /// </summary>
    public FlociContainerResource Parent { get; } = floci ?? throw new ArgumentNullException(nameof(floci));

    /// <summary>
    /// Gets the http endpoint for the Floci UI resource.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
}

#pragma warning restore ASPIREATS001
