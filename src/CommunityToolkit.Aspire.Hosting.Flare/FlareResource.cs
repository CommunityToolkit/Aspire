// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Flare observability stack. The resource itself is the OTLP ingest
/// container; the query API, alert worker and dashboard are added as child resources.
/// </summary>
[AspireExport(ExposeProperties = true)]
public class FlareResource : ContainerResource, IResourceWithConnectionString
{
    internal const string OtlpGrpcEndpointName = "otlp-grpc";
    internal const string OtlpHttpEndpointName = "otlp-http";

    /// <summary>
    /// Initializes a new instance of the <see cref="FlareResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    public FlareResource([ResourceName] string name) : base(name)
    {
        OtlpGrpcEndpoint = new(this, OtlpGrpcEndpointName);
        OtlpHttpEndpoint = new(this, OtlpHttpEndpointName);
    }

    /// <summary>
    /// Gets the OTLP/gRPC endpoint of the ingest service (port 4317 by default).
    /// </summary>
    public EndpointReference OtlpGrpcEndpoint { get; }

    /// <summary>
    /// Gets the OTLP/HTTP endpoint of the ingest service (port 4318 by default).
    /// </summary>
    public EndpointReference OtlpHttpEndpoint { get; }

    /// <summary>
    /// Gets the query API child resource.
    /// </summary>
    [AspireExportIgnore(Reason = "Child resources are reached through the resource tree; they are not exported as handles.")]
    public FlareApiResource Api { get; internal set; } = default!;

    /// <summary>
    /// Gets the alert worker child resource.
    /// </summary>
    [AspireExportIgnore(Reason = "Child resources are reached through the resource tree; they are not exported as handles.")]
    public FlareAlertWorkerResource AlertWorker { get; internal set; } = default!;

    /// <summary>
    /// Gets the dashboard child resource.
    /// </summary>
    [AspireExportIgnore(Reason = "Child resources are reached through the resource tree; they are not exported as handles.")]
    public FlareDashboardResource Dashboard { get; internal set; } = default!;

    // The ingest API key set by WithApiKey, so WithOtlpExporter can send it from the consuming resource.
    internal ParameterResource? IngestApiKey { get; set; }

    /// <summary>
    /// Gets the connection string expression for the ingest service: the OTLP/gRPC URL, for example
    /// <c>http://localhost:4317</c>.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{OtlpGrpcEndpoint.Property(EndpointProperty.Url)}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{OtlpGrpcEndpoint.Property(EndpointProperty.Host)}"));
        yield return new("Port", ReferenceExpression.Create($"{OtlpGrpcEndpoint.Property(EndpointProperty.Port)}"));
        yield return new("Uri", ConnectionStringExpression);
    }
}

/// <summary>
/// A resource that represents the query API of a Flare stack.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="parent">The Flare resource this API belongs to.</param>
public class FlareApiResource([ResourceName] string name, FlareResource parent)
    : ContainerResource(name)
{
    internal const string HttpEndpointName = "http";

    /// <summary>
    /// Gets the Flare resource this resource is grouped under. This is a visual grouping only: the
    /// resource does not share a lifecycle with it.
    /// </summary>
    public FlareResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));
}

/// <summary>
/// A resource that represents the alert evaluation worker of a Flare stack.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="parent">The Flare resource this worker belongs to.</param>
public class FlareAlertWorkerResource([ResourceName] string name, FlareResource parent)
    : ContainerResource(name)
{
    /// <summary>
    /// Gets the Flare resource this resource is grouped under. This is a visual grouping only: the
    /// resource does not share a lifecycle with it.
    /// </summary>
    public FlareResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));
}

/// <summary>
/// A resource that represents the dashboard of a Flare stack.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="parent">The Flare resource this dashboard belongs to.</param>
public class FlareDashboardResource([ResourceName] string name, FlareResource parent)
    : ContainerResource(name)
{
    internal const string HttpEndpointName = "http";

    /// <summary>
    /// Gets the Flare resource this resource is grouped under. This is a visual grouping only: the
    /// resource does not share a lifecycle with it.
    /// </summary>
    public FlareResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets the HTTP endpoint of the dashboard.
    /// </summary>
    public EndpointReference PrimaryEndpoint => field ??= new(this, HttpEndpointName);
}
