#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Floci AWS emulator container.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class FlociContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const int AwsEndpointPort = 4566;
    internal const string AwsEndpointName = "aws";
    internal const string HostnameEnvVar = "FLOCI_HOSTNAME";
    internal const string DefaultRegionEnvVar = "FLOCI_DEFAULT_REGION";
    internal const string DefaultAccountIdEnvVar = "FLOCI_DEFAULT_ACCOUNT_ID";
    internal const string StorageModeEnvVar = "FLOCI_STORAGE_MODE";
    internal const string DockerHostEnvVar = "FLOCI_DOCKER_DOCKER_HOST";
    internal const string TlsEnabledEnvVar = "FLOCI_TLS_ENABLED";
    internal const string TlsCertPathEnvVar = "FLOCI_TLS_CERT_PATH";
    internal const string TlsKeyPathEnvVar = "FLOCI_TLS_KEY_PATH";

    // Quarkus JVM Docker image config override path
    internal const string ConfigMountPath = "/deployments/config/application.yml";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets or sets whether TLS is enabled for this Floci instance.
    /// Set by the <c>WithHttpsCertificateConfiguration</c> callback registered inside <c>AddFloci</c>
    /// when any certificate (dev cert or custom) is configured on the builder.
    /// Affects <see cref="ConnectionStringExpression"/> and the AWS_ENDPOINT_URL injected into dependent resources.
    /// </summary>
    internal bool TlsEnabled { get; set; }

    /// <summary>
    /// Gets the AWS region configured for this Floci instance.
    /// Set by <see cref="FlociHostingExtension.AddFloci"/> from the <c>defaultRegion</c> parameter.
    /// Used by the <c>BeforeStartEvent</c> subscriber to inject <c>AWS_DEFAULT_REGION</c> into dependent resources.
    /// </summary>
    internal string DefaultRegion { get; init; } = "us-east-1";

    /// <summary>
    /// Gets the primary AWS endpoint reference for the Floci container.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, AwsEndpointName);


    /// <summary>
    /// Gets the host endpoint reference for the AWS endpoint.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for the AWS endpoint.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the AWS endpoint URL. Uses <c>https://</c> when a certificate is configured
    /// (via <c>WithHttpsDeveloperCertificate()</c> or <c>WithHttpsCertificate()</c>),
    /// otherwise <c>http://</c>. Both schemes connect to the same port (4566) when TLS is enabled.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        TlsEnabled
            ? ReferenceExpression.Create($"https://{Host}:{Port}")
            : ReferenceExpression.Create($"http://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", ConnectionStringExpression);
    }
}

#pragma warning restore ASPIREATS001
