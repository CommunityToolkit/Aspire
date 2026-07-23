#pragma warning disable ASPIREATS001 // AspireExport is still experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Floci UI web console container. A single UI instance can attach to any
/// combination of Floci cloud resources (AWS, Azure, GCP) — one created it via <c>WithFlociUI</c>
/// (its <see cref="Parent"/>), and any others are attached via <c>WithPluggedCloud</c>.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="floci">The Floci cloud resource that created this UI instance.</param>
[AspireExport(ExposeProperties = true)]
public class FlociUIContainerResource(string name, FlociContainerResource floci)
    : ContainerResource(name), IResourceWithParent<FlociContainerResource>
{
    // The packaged floci/floci-ui image serves both the SPA and its API from a single
    // server listening on PORT (default 4500).
    internal const int UIPort = 4500;
    internal const string PrimaryEndpointName = "http";

    // AWS adapter env vars.
    internal const string EndpointEnvVar = "FLOCI_ENDPOINT";
    internal const string RegionEnvVar = "AWS_REGION";
    internal const string AccessKeyIdEnvVar = "AWS_ACCESS_KEY_ID";
    internal const string SecretAccessKeyEnvVar = "AWS_SECRET_ACCESS_KEY";
    internal const string DefaultAccountIdEnvVar = "FLOCI_DEFAULT_ACCOUNT_ID";

    // Azure adapter env vars.
    internal const string AzureEndpointEnvVar = "FLOCI_AZURE_ENDPOINT";
    internal const string AzureAccountNameEnvVar = "FLOCI_AZURE_ACCOUNT_NAME";

    // GCP adapter env vars.
    internal const string GcpEndpointEnvVar = "FLOCI_GCP_ENDPOINT";
    internal const string GcpProjectEnvVar = "FLOCI_GCP_PROJECT";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the Floci cloud resource that created this UI instance.
    /// </summary>
    public FlociContainerResource Parent { get; } = floci ?? throw new ArgumentNullException(nameof(floci));

    /// <summary>
    /// Gets the http endpoint for the Floci UI resource.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
}

#pragma warning restore ASPIREATS001
