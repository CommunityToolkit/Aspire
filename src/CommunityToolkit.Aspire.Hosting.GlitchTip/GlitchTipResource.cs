// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Hosting.GlitchTip;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Management;

#pragma warning disable IDE0130 // Hosting extensions and resources use the Aspire public API namespaces.
namespace Aspire.Hosting.ApplicationModel;
#pragma warning restore IDE0130

/// <summary>A stack's single GlitchTip project, hosted locally or provisioned in shared infrastructure.</summary>
[AspireExport]
public sealed class GlitchTipResource : Resource, IResourceWithConnectionString
{
    internal GlitchTipResource(string name, ParameterResource projectSlug, ParameterResource release, string environmentName)
        : base(name)
    {
        ProjectSlug = projectSlug;
        Release = release;
        EnvironmentName = environmentName;
        Dsn = new GlitchTipDsnReference(this);
    }

    /// <summary>Gets the stable project slug parameter.</summary>
    public ParameterResource ProjectSlug { get; }
    /// <summary>Gets the default release parameter.</summary>
    public ParameterResource Release { get; }
    /// <summary>Gets the AppHost environment used for reporting and monitor ownership.</summary>
    public string EnvironmentName { get; }
    /// <summary>Gets the reporting URI expression, resolved by project provisioning.</summary>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{Dsn}");
    /// <summary>Gets the reporting connection string expression.</summary>
    public ReferenceExpression ConnectionStringExpression => UriExpression;

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() =>
        [new("Uri", UriExpression)];

    internal GlitchTipDsnReference Dsn { get; }
    internal TaskCompletionSource<GlitchTipProject> Provisioned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ContainerResource? LocalServer { get; set; }
    internal ParameterResource? InstanceUrl { get; set; }
    internal ParameterResource? Organization { get; set; }
    internal ParameterResource? InitialTeam { get; set; }
    internal ParameterResource? ApiToken { get; set; }
    internal List<ParameterResource> OwnedDeploymentParameters { get; } = [];
    internal ParameterResource? DisplayName { get; set; }
    internal ParameterResource? AdminEmail { get; set; }
    internal ParameterResource? AdminPassword { get; set; }
    internal GlitchTipMonitorOptions MonitorDefaults { get; set; } = new();
    internal bool LocalArtifactUploadsEnabled { get; set; }
    internal (Uri Instance, string Token, string Organization)? Management { get; set; }
}

internal sealed class GlitchTipDsnReference(GlitchTipResource resource) : IValueProvider, IManifestExpressionProvider, IValueWithReferences
{
    public string ValueExpression => $"{{{resource.Name}.connectionString}}";
    public IEnumerable<object> References => resource.LocalServer is { } server ? [server] : [];
    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default) => GetValueAsync(new ValueProviderContext(), cancellationToken);

    public async ValueTask<string?> GetValueAsync(ValueProviderContext context, CancellationToken cancellationToken = default)
    {
        var project = await resource.Provisioned.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (resource.LocalServer is not { } server)
        {
            return project.Dsn;
        }

        // Preserve the key and project path but resolve networking from the consuming resource's context.
        var endpoint = new EndpointReference(server, "http");
        var address = await ((IValueProvider)endpoint).GetValueAsync(context, cancellationToken).ConfigureAwait(false);
        var target = new Uri(address!);
        return new UriBuilder(project.Dsn) { Scheme = target.Scheme, Host = target.Host, Port = target.Port }.Uri.AbsoluteUri;
    }
}